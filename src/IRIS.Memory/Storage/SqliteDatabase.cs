using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace IRIS.Memory.Storage;

[SupportedOSPlatform("windows")]
internal static class SqliteNative
{
    private const string Library = "winsqlite3.dll";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr database, int flags, IntPtr vfs);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_close_v2(IntPtr database);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_busy_timeout(DatabaseHandle database, int milliseconds);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_limit(DatabaseHandle database, int category, int value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_prepare_v2(DatabaseHandle database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int size, out IntPtr statement, out IntPtr tail);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_finalize(IntPtr statement);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_bind_text(StatementHandle statement, int index, byte[] text, int length, IntPtr destructor);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_bind_int64(StatementHandle statement, int index, long value);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_step(StatementHandle statement);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr sqlite3_column_text(StatementHandle statement, int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_column_bytes(StatementHandle statement, int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern long sqlite3_column_int64(StatementHandle statement, int index);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int sqlite3_changes(DatabaseHandle database);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void sqlite3_interrupt(DatabaseHandle database);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr sqlite3_libversion();
}

[SupportedOSPlatform("windows")]
internal sealed class DatabaseHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DatabaseHandle(IntPtr value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle() => SqliteNative.sqlite3_close_v2(handle) == 0;
}
[SupportedOSPlatform("windows")]
internal sealed class StatementHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public StatementHandle(IntPtr value) : base(true) => SetHandle(value);
    protected override bool ReleaseHandle() { SqliteNative.sqlite3_finalize(handle); return true; }
}

public sealed class MemoryStorageException(int code) : IOException("Le stockage mémoire est indisponible (SQLite " + code + "). Les données existantes sont conservées.")
{
    public int Code { get; } = code;
}

[SupportedOSPlatform("windows")]
internal sealed class SqliteStatement : IDisposable
{
    private readonly StatementHandle handle;
    public SqliteStatement(DatabaseHandle database, string sql, params object[] parameters)
    {
        int result = SqliteNative.sqlite3_prepare_v2(database, sql, -1, out IntPtr pointer, out _);
        if (result != 0) { if (pointer != IntPtr.Zero) SqliteNative.sqlite3_finalize(pointer); throw new MemoryStorageException(result); }
        handle = new(pointer);
        try
        {
            for (int index = 0; index < parameters.Length; index++)
            {
                result = parameters[index] switch
                {
                    string text => BindText(index + 1, text),
                    long number => SqliteNative.sqlite3_bind_int64(handle, index + 1, number),
                    int number => SqliteNative.sqlite3_bind_int64(handle, index + 1, number),
                    _ => throw new ArgumentException("Type de paramètre SQLite interdit.")
                };
                if (result != 0) throw new MemoryStorageException(result);
            }
        }
        catch { handle.Dispose(); throw; }
    }
    private int BindText(int index, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
        return SqliteNative.sqlite3_bind_text(handle, index, bytes, bytes.Length - 1, new IntPtr(-1));
    }
    public bool Read()
    {
        int result = SqliteNative.sqlite3_step(handle);
        if (result is 100 or 101) return result == 100;
        throw new MemoryStorageException(result);
    }
    public long Number(int column) => SqliteNative.sqlite3_column_int64(handle, column);
    public string Text(int column)
    {
        IntPtr text = SqliteNative.sqlite3_column_text(handle, column);
        int length = SqliteNative.sqlite3_column_bytes(handle, column);
        if (length > 8192) throw new InvalidDataException("Champ mémoire trop volumineux.");
        return text == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(text, length) ?? "";
    }
    public void Dispose() => handle.Dispose();
}

[SupportedOSPlatform("windows")]
internal sealed class SqliteDatabase : IDisposable
{
    private readonly DatabaseHandle handle;
    public static string Version => Marshal.PtrToStringUTF8(SqliteNative.sqlite3_libversion()) ?? "inconnue";
    public SqliteDatabase(string path)
    {
        int result = SqliteNative.sqlite3_open_v2(path, out IntPtr pointer, 2 | 4 | 0x10000, IntPtr.Zero);
        handle = new(pointer);
        if (result != 0) { handle.Dispose(); throw new MemoryStorageException(result); }
        try
        {
            result = SqliteNative.sqlite3_busy_timeout(handle, 2000);
            if (result != 0) throw new MemoryStorageException(result);
            SqliteNative.sqlite3_limit(handle, 0, 2097152);
            SqliteNative.sqlite3_limit(handle, 1, 16384);
            Execute("PRAGMA trusted_schema=OFF");
            if (Scalar("PRAGMA trusted_schema") != 0) throw new InvalidDataException("Cette version SQLite ne permet pas de désactiver les schémas de confiance.");
            EnsureSchema();
            Execute("PRAGMA foreign_keys=ON");
            Execute("PRAGMA journal_mode=DELETE");
            Execute("PRAGMA synchronous=FULL");
            Execute("PRAGMA secure_delete=ON");
            if (Scalar("PRAGMA secure_delete") != 1) throw new InvalidDataException("La purge SQLite demandée est indisponible.");
            Execute("PRAGMA temp_store=MEMORY");
        }
        catch { handle.Dispose(); throw; }
    }

    private void EnsureSchema()
    {
        long version = Scalar("PRAGMA user_version");
        long application = Scalar("PRAGMA application_id");
        if (version == 1 && application == 0x49524953) return;
        if (version != 0 || application != 0 || Scalar("SELECT COUNT(*) FROM sqlite_master") != 0)
            throw new InvalidDataException("Base mémoire étrangère ou version incompatible ; aucun remplacement automatique.");
        Transaction(() =>
        {
            Execute("CREATE TABLE memory_policy (id INTEGER PRIMARY KEY CHECK(id=1), enabled INTEGER NOT NULL CHECK(enabled IN (0,1)), share INTEGER NOT NULL CHECK(share IN (0,1) AND share<=enabled), retention INTEGER NOT NULL CHECK(retention BETWEEN 1 AND 3650))");
            Execute("INSERT INTO memory_policy VALUES (1,0,0,90)");
            Execute("CREATE TABLE memory_state (id INTEGER PRIMARY KEY CHECK(id=1), revision INTEGER NOT NULL CHECK(revision>=0))");
            Execute("INSERT INTO memory_state VALUES (1,0)");
            Execute("CREATE TABLE memories (id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL CHECK(length(title) BETWEEN 1 AND 80), content TEXT NOT NULL CHECK(length(content) BETWEEN 1 AND 1000), kind INTEGER NOT NULL CHECK(kind BETWEEN 0 AND 2), created INTEGER NOT NULL, updated INTEGER NOT NULL, expires INTEGER NOT NULL CHECK(expires>updated), revision INTEGER NOT NULL CHECK(revision>0), search_text TEXT NOT NULL)");
            Execute("CREATE INDEX memories_expiration ON memories(expires)");
            Execute("PRAGMA application_id=1230129491");
            Execute("PRAGMA user_version=1");
        });
    }
    public SqliteStatement Query(string sql, params object[] parameters) => new(handle, sql, parameters);
    public long Scalar(string sql, params object[] parameters)
    {
        using var statement = Query(sql, parameters);
        if (!statement.Read()) throw new InvalidDataException("Métadonnée mémoire absente.");
        return statement.Number(0);
    }
    public int Execute(string sql, params object[] parameters)
    {
        using var statement = Query(sql, parameters);
        while (statement.Read()) { }
        return SqliteNative.sqlite3_changes(handle);
    }
    public T Transaction<T>(Func<T> work)
    {
        Execute("BEGIN IMMEDIATE");
        try { T result = work(); Execute("COMMIT"); return result; }
        catch { try { Execute("ROLLBACK"); } catch (MemoryStorageException) { } throw; }
    }
    public void Transaction(Action work) => Transaction(() => { work(); return 0; });
    public void Interrupt() => SqliteNative.sqlite3_interrupt(handle);
    public void Dispose() => handle.Dispose();
}
