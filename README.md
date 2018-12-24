# Syncrypt

Syncrypt is a folder sync tool with PGP encryption. It provides a left->right synchronisation with file level encryption.

### Installing

Extract the content of the zip file to your computer.

### Getting started

Generate a keypair
```
syncrypt.exe -g -k C:\keys -p "passphrase"
```

Run synchronisation
```
syncrypt.exe -i C:\docs -o C:\docs_encrypted -k C:\keys
```

Refer to the [Wiki] for more options.

## Built With

* [Commandlineparser](https://github.com/commandlineparser/commandline) - Command line argument parser
* [PgpCore](https://github.com/mattosaurus/PgpCore) - Pgp encryption
* [System.Data.SQLite](https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki) - For the SQLite database

## License

This project is licensed under the MIT License
