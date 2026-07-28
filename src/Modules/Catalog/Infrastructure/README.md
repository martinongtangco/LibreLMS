EF Core persistence (and, for Scorm, the Redis/Valkey session store) for **Catalog**.
Implements the abstractions Application defines. This is the only layer allowed to
know about MSSQL/Valkey connection details.
