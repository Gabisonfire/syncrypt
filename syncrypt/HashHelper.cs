using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;

namespace syncrypt
{
    class HashHelper
    {
        const string DATABASE = "syncrypt.sqlite";
        public static SQLiteConnection c; 

        public static void CreateDB()
        {
            if (!File.Exists(DATABASE))
            {
                SQLiteConnection.CreateFile(DATABASE);
            }
        }

        public static void InitDB()
        {
            if (File.Exists(DATABASE))
            {
                SQLiteCommand cmd = new SQLiteCommand();
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS files (id integer PRIMARY KEY, filename text NOT NULL UNIQUE, hash text NOT NULL UNIQUE, timestamp text NOT NULL, deleted integer DEFAULT 0);";
                Connection();
                WriteQuery(cmd);
                c.Close();
            } 
        }

        public static void Connection()
        {
            try
            {
                c = new SQLiteConnection("Data source=" + DATABASE + ";Version=3;");
                c.Open();
            }
            catch (Exception ex)
            {
                Program.Log(ex.ToString(), Program.LEVEL.Error);
            }
        }

        public static void WriteQueries(List<SQLiteCommand> queries)
        {
            SQLiteTransaction tr = null;
            try
            {
                tr = c.BeginTransaction();
                foreach (SQLiteCommand query in queries)
                {
                    query.Transaction = tr;
                    query.ExecuteNonQuery();
                }
                tr.Commit();
            }
            catch(Exception ex)
            {
                Program.Log(ex.ToString(), Program.LEVEL.Error);
                tr.Rollback();
            }
        }

        public static void WriteQuery(SQLiteCommand query)
        {
            SQLiteTransaction tr = null;
            try
            {
                tr = c.BeginTransaction();
                query.Transaction = tr;
                query.ExecuteNonQuery();
                tr.Commit();
            }
            catch (Exception ex)
            {
                Program.Log(ex.ToString(), Program.LEVEL.Error);
                tr.Rollback();
            }
        }

        public static Dictionary<string,string> ReadSingleQuery(SQLiteCommand query, string[] columns)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            query.Connection = c;
            using (SQLiteDataReader rdr = query.ExecuteReader())
            {
                while(rdr.Read())
                {
                    foreach(string column in columns)
                    {
                        result.Add(column, rdr[column].ToString());
                    }
                }

            }
            return result;
        }

        public static string[] ReadMultipleQuery(SQLiteCommand query)
        {
            query.Connection = c;
            List<string> result = new List<string>();
            using (SQLiteDataReader rdr = query.ExecuteReader())
            {
                while (rdr.Read())
                {
                    result.Add(rdr.GetString(0));
                }

            }
            return result.ToArray();
        }

        public static string GetHashFromFile(string filename)
        {
            MD5 md5 = MD5.Create();
            using (var stream = File.OpenRead(filename))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();              
            }
        }

        public static void StoreHash(string filename, string hashed, bool update = false)
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            if (update)
            {
                cmd.CommandText = "UPDATE files SET hash = @hash,  timestamp = CURRENT_TIMESTAMP WHERE filename = @filename";
            }
            else
            {                
                cmd.CommandText = "INSERT INTO files (filename, hash, timestamp) VALUES (@filename, @hash, CURRENT_TIMESTAMP)";
            }
            cmd.Parameters.Add(new SQLiteParameter("@filename", filename));
            cmd.Parameters.Add(new SQLiteParameter("@hash", hashed));
            WriteQuery(cmd);
        }

        public static string GetHashFromDb(string filename)
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "SELECT hash FROM files WHERE filename = @filename";
            cmd.Parameters.Add(new SQLiteParameter("@filename", filename));
            return ReadSingleQuery(cmd, new string[] { "hash" }).TryGetValue("hash", out string value) ? value : null;
        }

        public static bool FindDupeHash(string hash)
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "SELECT filename, hash FROM files WHERE hash = @hash";
            cmd.Parameters.Add(new SQLiteParameter("@hash", hash));
            string result = ReadSingleQuery(cmd, new string[] { "hash" }).TryGetValue("hash", out string value) ? value : null;
            if ( result == null)
            {
                return false;
            }
            if ( result == hash)
            {
                return true;
            }
            else
            {
                Program.Log("Unexpected result from query: " + result, Program.LEVEL.Error);
                return false;
            }
        }

        public static bool FindDupeFileName(string filename)
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "SELECT filename, hash FROM files WHERE filename = @filename";
            cmd.Parameters.Add(new SQLiteParameter("@filename", filename));
            string result = ReadSingleQuery(cmd, new string[] { "filename" }).TryGetValue("filename", out string value) ? value : null;
            if (result == null)
            {
                return false;
            }
            if (result == filename)
            {
                return true;
            }
            else
            {
                Program.Log("Unexpected result from query: " + result, Program.LEVEL.Error);
                return false;
            }
        }

        public static string[] GetDbFilesList()
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "SELECT filename FROM files";
            return ReadMultipleQuery(cmd);
        }

        public static void MarkForDeletion(string filename)
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "UPDATE files SET deleted = 1 WHERE filename = @filename";
            cmd.Parameters.Add(new SQLiteParameter("@filename", filename));
            WriteQuery(cmd);
        }

        public static string[] GetDeletedFiles()
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "SELECT filename FROM files WHERE deleted = 1";
            return ReadMultipleQuery(cmd);
        }

        public static void DeleteFromDb(string filename)
        {
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = "DELETE FROM files WHERE filename = @filename";
            cmd.Parameters.Add(new SQLiteParameter("@filename", filename));
            WriteQuery(cmd);
        }
    }
}
