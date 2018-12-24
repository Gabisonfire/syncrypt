using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using System.IO;
using PgpCore;
using System.Timers;

namespace syncrypt
{
    class Program
    {
        public static string PRIVATE_KEY = "";
        public static string PUBLIC_KEY = "";

        public static LEVEL LogLevel = LEVEL.Error;
        public static bool Quiet = false;
        public static bool VeryQuiet = false;


        public static Timer timer = new Timer();

        public class Options
        {
            [Option('i', "input", Required = false, HelpText = "Specify the input directory.")]
            public string Inputdir { get; set; }

            [Option('o', "output", Required = false, HelpText = "Specify the output directory.")]
            public string Ouputdir { get; set; }

            [Option('g', "generate-key", Required = false, HelpText = "Generate a new keypair for encyption.")]
            public bool KeyGen { get; set; }

            [Option('k', "key-path", Required = false, HelpText = "Specifies the path for the keys.")]
            public string KeyPath { get; set; }

            [Option('d', "decrypt", Required = false, HelpText = "Only decrpyt files.")]
            public bool Decrypt { get; set; }

            [Option('D', "daemon", Required = false, HelpText = "Run as daemon.")]
            public bool Daemon { get; set; }

            [Option('p', "password", Required = false, HelpText = "Private key passphrase.", Default = "")]
            public string Password { get; set; }

            [Option('t', "timer", Required = false, HelpText = "File rescan time in milliseconds.")]
            public int TimerSecs { get; set; }

            [Option('q', "quiet", Required = false, HelpText = "Mute console output except errors.")]
            public bool Quiet { get; set; }

            [Option("propagate-deletions", Required = false, HelpText = "Automatically deletes files flagged for deletions")]
            public bool PropagateDeletions { get; set; }

            [Option("review-deletions", Required = false, HelpText = "Review files flagged for deletions")]
            public bool ReviewDeletions { get; set; }

            [Option("apply-deletions", Required = false, HelpText = "Deletes files flagged for deletions")]
            public bool ApplyDeletions { get; set; }

            [Option("log-warnings", Required = false, HelpText = "Log warning messages and below(log file)")]
            public bool LogWarnings { get; set; }

            [Option("log-info", Required = false, HelpText = "Log information messages and below(log file)")]
            public bool LogInfo { get; set; }

            [Option("very-quiet", Required = false, HelpText = "Mute all console output.")]
            public bool VeryQuiet{ get; set; }

            [Option("public-key", Required = false, HelpText = "Public key filename", Default = "/public.syncrypt.key")]
            public string PublicKey { get; set; }

            [Option("private-key", Required = false, HelpText = "Private key filename", Default = "/private.syncrypt.key")]
            public string PrivateKey { get; set; }

        }

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
              .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
              .WithNotParsed<Options>(errs => HandleParseError(errs));                
        }

        private static void RunOptionsAndReturnExitCode(Options opts)
        {
            PrintAppInfo();
            HashHelper.CreateDB();
            HashHelper.InitDB();
            HashHelper.Connection();

            // Setting global variables
            if (opts.LogWarnings) LogLevel = LEVEL.Warning;
            if (opts.LogInfo) LogLevel = LEVEL.Info;
            Quiet = opts.Quiet;
            VeryQuiet = opts.VeryQuiet;
            PUBLIC_KEY = opts.PublicKey;
            PRIVATE_KEY = opts.PrivateKey;

            if (opts.Daemon)
            {
                Log("Running as daemon", LEVEL.Info);                
                if (opts.TimerSecs < 1) opts.TimerSecs = 1;
                timer.Interval = opts.TimerSecs * 1000;
                timer.Elapsed += new ElapsedEventHandler((sender, e) => ProcessFiles(opts));
                timer.Start();
                Log("Press Enter to quit.");
                Console.Read();
            }
            else
            {
                ProcessFiles(opts);
            }
        }

        private static void ProcessFiles(Options opts)
        {
            timer.Stop();
            PGP pgp = new PGP();

            // Key generation
            if (opts.KeyGen)
            {
                if (opts.Daemon)
                {
                    Log("Can't generate keys when running as daemon.", LEVEL.Error);
                    Environment.Exit(1);
                }
                if (!File.Exists(opts.KeyPath + PUBLIC_KEY) || !File.Exists(opts.KeyPath + PRIVATE_KEY))
                {

                    try
                    {
                        pgp.GenerateKey(opts.KeyPath + PUBLIC_KEY, opts.KeyPath + PRIVATE_KEY, password: opts.Password);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString(), LEVEL.Error);
                        Environment.Exit(1);
                    }
                    Log("Keypair written to " + opts.KeyPath + "/", LEVEL.Info);
                    Environment.Exit(0);
                }
                else
                {
                    Log("Key files already present, will not overwrite for security reasons. Exiting", LEVEL.Error);
                    Environment.Exit(1);
                }
            }

            // Reviewing Deletions
            if(opts.ReviewDeletions)
            {
                foreach (string x in HashHelper.GetDeletedFiles())
                {
                    Log(x);
                }
                Environment.Exit(0);
            }

            if(opts.ApplyDeletions)
            {
                DeleteFiles(opts, pgp);         // Pgp passed because we need to decrypt the file before deleting.
                Environment.Exit(0);
            }

            // File Handling
            Log("Searching files and directories...");
            string[] files = Directory.GetFiles(opts.Inputdir, "*", SearchOption.AllDirectories);
            string[] dirs = Directory.GetDirectories(opts.Inputdir, "*", SearchOption.AllDirectories);


            Log("Creating directories...");
            foreach (string dir in dirs)
            {
                try
                {
                    if (!Directory.Exists(dir.Replace(opts.Inputdir, opts.Ouputdir)))
                    {
                        try
                        {
                            Directory.CreateDirectory(dir.Replace(opts.Inputdir, opts.Ouputdir));
                            Log("Created " + dir.Replace(opts.Inputdir, opts.Ouputdir), LEVEL.Info);
                        }
                        catch (Exception ex)
                        {
                            Log(ex.ToString(), LEVEL.Error);
                            Environment.Exit(1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.ToString(), LEVEL.Error);
                    Environment.Exit(1);
                }
            }


            Log("Using keyfile: " + opts.KeyPath + PRIVATE_KEY, LEVEL.Info);
            Log("Processing " + files.Count().ToString() + " files.", LEVEL.Info);
            int total = files.Count();
            int count = 1;
            foreach (string file in files)
            {
                Log(count.ToString() + "/" + total.ToString());
                if (opts.Decrypt)
                {
                    Log("Decrypting " + file);
                    try
                    {
                        pgp.DecryptFile(file, opts.Ouputdir + "/" + Path.GetFileName(file).Replace(".pgp", ""), opts.KeyPath + PRIVATE_KEY, opts.Password);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString(), LEVEL.Error);
                    }
                    Log("Wrote " + opts.Ouputdir + "/" + Path.GetFileName(file), LEVEL.Ok);
                }
                else
                {                    
                    string fileHash = HashHelper.GetHashFromFile(file);
                    if (HashHelper.GetHashFromDb(file) != fileHash)
                    {
                        bool update = false;
                        if (HashHelper.FindDupeHash(fileHash))
                        {
                            Log("Found an existing hash for file: " + file + ", skipping.", LEVEL.Warning);
                        }
                        else
                        {
                            if (HashHelper.FindDupeFileName(file))
                            {
                                update = true;
                                Log("Found another file with the same name, updating hash and re-encrypting.", LEVEL.Info);
                            }                            
                            try
                            {
                                Log("Encrypting " + file, LEVEL.Info);
                                pgp.EncryptFile(file, file.Replace(opts.Inputdir, opts.Ouputdir) + ".pgp", opts.KeyPath + PUBLIC_KEY);
                                Log("Storing hash " + fileHash + " for " + file, LEVEL.Info);
                                HashHelper.StoreHash(file, fileHash, update);
                                Log("Wrote " + file.Replace(opts.Inputdir, opts.Ouputdir) + ".pgp", LEVEL.Ok);
                            }
                            catch(Exception ex)
                            {
                                Log(ex.ToString(), LEVEL.Error);
                            }
                        }
                    } else
                    {
                        Log("Skipping " + file + " -> Unchanged.");
                    }
                }
                count++;
            }

            // Exit after loop if only used for decryption
            if (opts.Decrypt)
            {
                Environment.Exit(0);
            }

            // Check deletions
            Log("Marking files for deletions...", LEVEL.Info);
            string[] dbFiles = HashHelper.GetDbFilesList();
            foreach(string file in dbFiles)
            {
                if(files.ToList().IndexOf(file) < 0)
                {
                    HashHelper.MarkForDeletion(file);
                    Log("File " + file + " is not in the source directory and was marked for deletion.");
                }
            }

            // Delete
            if(opts.PropagateDeletions)
            {
                DeleteFiles(opts, pgp);
            }

            Log("Complete.", LEVEL.Info);
            timer.Start();
        }

        private static void PrintAppInfo()
        {
            Log("Syncrypt " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
            Log("Log level is set to: " + LogLevel.ToString());
        }

        private static void DeleteFiles(Options opts, PGP pgp)
        {
            Log("Deleting removed files...", LEVEL.Info);
            string[] delete = HashHelper.GetDeletedFiles();
            foreach (string f in delete)
            {
                string hash = HashHelper.GetHashFromDb(f);
                HashHelper.DeleteFromDb(f);
                string encFile = opts.Ouputdir + "/" + Path.GetFileName(f) + ".pgp";

                if (File.Exists(encFile))
                {
                    // Create a temp directory for decryption
                    DirectoryInfo dir = null;
                    try
                    {
                        dir = Directory.CreateDirectory("temp");
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString(), LEVEL.Error);
                        Environment.Exit(1);
                    }
                    Log("Created temp directory for decryption: " + dir.FullName, LEVEL.Info);

                    // Decrypt and get file hash
                    try
                    {
                        pgp.DecryptFile(encFile, dir.FullName + "/" + Path.GetFileName(f).Replace(".pgp", ""), opts.KeyPath + PRIVATE_KEY, opts.Password);
                    }
                    catch (Org.BouncyCastle.Bcpg.OpenPgp.PgpException pex)
                    {
                        Log("Decrypting exception. (Did you provide the private key passphrase?) " + pex.ToString(), LEVEL.Error);
                        Environment.Exit(1);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString(), LEVEL.Error);
                        Environment.Exit(1);
                    }

                    if (hash == HashHelper.GetHashFromFile(dir.FullName + "/" + Path.GetFileName(f)))
                    {
                        try
                        {
                            File.Delete(encFile);
                        }
                        catch(Exception ex)
                        {
                            Log(ex.ToString(), LEVEL.Error);
                            continue;
                        }
                        Log("Deleted encrpyted file: " + encFile);
                    }
                    else
                    {
                        Log("Hashes of encrypted file is not matching the original. The file will be deleted from the database but not from the disk.", LEVEL.Warning);
                    }

                    //Cleanup
                    try
                    {
                        Directory.Delete(dir.FullName, true);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString(), LEVEL.Error);
                    }
                }
            }
        }

        private static void HandleParseError(object errs)
        {
            return;
        }

        public enum LEVEL { Console = 0, Info = 1, Ok = 2, Warning = 3, Error = 4 };

        public static void Log(string msg, LEVEL level = LEVEL.Console)
        {
            string msg_prefix = "[" + level.ToString() + "]" + " :: " + DateTime.Now.ToLongTimeString() + " - ";
            msg = msg_prefix + msg + Environment.NewLine;
            string LogDir = "logs";
            string LogFile = LogDir + "/" + "syncrypt.log";
            string LogFileError = LogDir + "/" + "syncrypt_error.log";

            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }
            if(level == LEVEL.Console)
            {
                if (!Quiet)
                {
                    Console.Write(msg);
                    Console.ResetColor();
                }
            }
            if(level == LEVEL.Info)
            {
                if (!Quiet)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(msg);
                    Console.ResetColor();
                }
                if(LogLevel <= LEVEL.Info)
                {
                    File.AppendAllText(LogFile, msg);
                };
            }
            if (level == LEVEL.Ok)
            {
                if (!Quiet)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(msg);
                    Console.ResetColor();
                }
                if (LogLevel <= LEVEL.Ok)
                {
                    File.AppendAllText(LogFile, msg);
                }
            }
            if (level == LEVEL.Warning)
            {
                if (!Quiet)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(msg);
                    Console.ResetColor();
                }
                if (LogLevel <= LEVEL.Warning)
                {
                    File.AppendAllText(LogFile, msg);
                }
            }
            if(level == LEVEL.Error)
            {
                if (!VeryQuiet)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(msg);
                    Console.ResetColor();
                }
                if (LogLevel <= LEVEL.Error)
                {
                    File.AppendAllText(LogFileError, msg);
                }
            }

        }
    }
}
