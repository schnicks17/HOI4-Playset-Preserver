// Minimal SQLite access through winsqlite3.dll, which ships with Windows 10 and 11.
// Lets the preserver read and write the launcher database without Python or extra DLLs.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class PPSqlite
{
    const string Dll = "winsqlite3.dll";
    const int SQLITE_OK = 0, SQLITE_ROW = 100, SQLITE_DONE = 101;
    const int SQLITE_INTEGER = 1, SQLITE_FLOAT = 2, SQLITE_NULL = 5;
    const int OPEN_READONLY = 0x1, OPEN_READWRITE = 0x2;
    static readonly IntPtr TRANSIENT = new IntPtr(-1);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_close_v2(IntPtr db);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_busy_timeout(IntPtr db, int ms);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)] static extern int sqlite3_prepare16_v2(IntPtr db, string sql, int nBytes, out IntPtr stmt, IntPtr tail);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_step(IntPtr stmt);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_column_count(IntPtr stmt);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern IntPtr sqlite3_column_name16(IntPtr stmt, int i);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_column_type(IntPtr stmt, int i);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern long sqlite3_column_int64(IntPtr stmt, int i);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern double sqlite3_column_double(IntPtr stmt, int i);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern IntPtr sqlite3_column_text16(IntPtr stmt, int i);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)] static extern int sqlite3_bind_text16(IntPtr stmt, int i, string val, int nBytes, IntPtr destructor);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_bind_int64(IntPtr stmt, int i, long val);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_bind_null(IntPtr stmt, int i);
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern IntPtr sqlite3_errmsg16(IntPtr db);

    static IntPtr Open(string path, bool write)
    {
        IntPtr db;
        byte[] name = System.Text.Encoding.UTF8.GetBytes(path + "\0");
        int rc = sqlite3_open_v2(name, out db, write ? OPEN_READWRITE : OPEN_READONLY, IntPtr.Zero);
        if (rc != SQLITE_OK) { string m = Err(db); sqlite3_close_v2(db); throw new Exception("Cannot open launcher database: " + m); }
        sqlite3_busy_timeout(db, 3000);
        return db;
    }

    static string Err(IntPtr db) { return db == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringUni(sqlite3_errmsg16(db)); }

    static IntPtr Prepare(IntPtr db, string sql, object[] args)
    {
        IntPtr st;
        if (sqlite3_prepare16_v2(db, sql, -1, out st, IntPtr.Zero) != SQLITE_OK) throw new Exception("SQL error: " + Err(db) + "\n" + sql);
        for (int i = 0; args != null && i < args.Length; i++)
        {
            object a = args[i];
            if (a == null) sqlite3_bind_null(st, i + 1);
            else if (a is int || a is long) sqlite3_bind_int64(st, i + 1, Convert.ToInt64(a));
            else sqlite3_bind_text16(st, i + 1, a.ToString(), -1, TRANSIENT);
        }
        return st;
    }

    // Rows as dictionaries (column name -> string, long, double or null)
    public static List<Dictionary<string, object>> Query(string path, string sql, object[] args)
    {
        var rows = new List<Dictionary<string, object>>();
        IntPtr db = Open(path, false);
        try
        {
            IntPtr st = Prepare(db, sql, args);
            try
            {
                int n = sqlite3_column_count(st);
                while (true)
                {
                    int rc = sqlite3_step(st);
                    if (rc == SQLITE_DONE) break;
                    if (rc != SQLITE_ROW) throw new Exception("SQL error: " + Err(db));
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < n; i++)
                    {
                        string col = Marshal.PtrToStringUni(sqlite3_column_name16(st, i));
                        int t = sqlite3_column_type(st, i);
                        if (t == SQLITE_NULL) row[col] = null;
                        else if (t == SQLITE_INTEGER) row[col] = sqlite3_column_int64(st, i);
                        else if (t == SQLITE_FLOAT) row[col] = sqlite3_column_double(st, i);
                        else row[col] = Marshal.PtrToStringUni(sqlite3_column_text16(st, i));
                    }
                    rows.Add(row);
                }
            }
            finally { sqlite3_finalize(st); }
        }
        finally { sqlite3_close_v2(db); }
        return rows;
    }

    // Runs several statements in one transaction; everything or nothing is written
    public static void ExecAll(string path, string[] sqls, object[][] args)
    {
        IntPtr db = Open(path, true);
        try
        {
            Step(db, "BEGIN IMMEDIATE;", null);
            try
            {
                for (int i = 0; i < sqls.Length; i++) Step(db, sqls[i], args[i]);
                Step(db, "COMMIT;", null);
            }
            catch { try { Step(db, "ROLLBACK;", null); } catch { } throw; }
        }
        finally { sqlite3_close_v2(db); }
    }

    static void Step(IntPtr db, string sql, object[] args)
    {
        IntPtr st = Prepare(db, sql, args);
        try
        {
            int rc = sqlite3_step(st);
            if (rc != SQLITE_DONE && rc != SQLITE_ROW) throw new Exception("SQL error: " + Err(db) + "\n" + sql);
        }
        finally { sqlite3_finalize(st); }
    }
}
