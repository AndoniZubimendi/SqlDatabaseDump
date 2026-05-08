# SqlDatabaseDump

Command line utility to dump SQL Server database structure to files. Built with .NET 9. Multi-threading by default.

## Building

Publish the app in release mode with dependencies included:

```
Publish.sh
```

## Usage

```
$ SqlDatabaseDump --server <host> -u <user> -p <pass> --database <db> --dir <dir>

or

$ SqlDatabaseDump -c <config_file>
```

Options:

```
	                                          Required:
	                                            -c  --config <ini>         Config file
	                                        
	                                          Or:
	                                            -s, --server <server>      SQL Server to connect to           (or DB_SERVER environment variable)
	                                            -u, --username <login>     Username to login                  (or DB_USERNAME environment variable)
	                                            -p, --password <pass>      Password for login                 (or DB_PASSWORD environment variable)
	                                            -d, --database <db>        Database to process                (or DB_DATABASE environment variable)
	                                            -o, --dir <dir>            Output directory                   (or DB_DIR environment variable)
	                                          Options:
	                                            -r, --reference-tables <table1,table2>  Reference tables to include (comma separated)
	                                            -e, --extended-properties               Include extended properties
	                                            -w, --with-dependencies                 Include dependencies
	                                            -a, --all                               Include all extras (extended properties and dependencies)
	                                            -r, --replace                           Replace existing files (default is to fail if file exists)
	                                            -s, --single-thread                     Single thread processing
	                                            -p, --parallel <n>                      Maximum parallel tasks 1..16 (default is 8)
	                                            -k, --skip-errors                       Skip errors without writing to file
	                                            -h, --help, -?                          Help information
```

Sample ini file

```
Server=my_server_ip
Login=my_user
Password=my_pass
Database=my_database
OutputDirectory=outputdir
;ReferenceTables=reftable1,reftable2
;ReplaceExistingFiles=false
;MaxParallel=8
;SingleThread=false
;SkipErrors=false
;ExtendedProperties=false
;WithDependencies=false
;AllExtras=false
```