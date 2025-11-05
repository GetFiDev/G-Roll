using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class MiniJson
{
    public static string Serialize(object obj)
    {
        var sb = new StringBuilder();
        SerializeValue(obj, sb);
        return sb.ToString();
    }

    static void SerializeValue(object value, StringBuilder sb)
    {
        if (value == null)
            sb.Append("null");
        else if (value is string s)
            sb.AppendFormat("\"{0}\"", EscapeString(s));
        else if (value is bool b)
            sb.Append(b ? "true" : "false");
        else if (value is IDictionary dict)
            SerializeObject(dict, sb);
        else if (value is IList list)
            SerializeArray(list, sb);
        else if (value is double or float or int or long or decimal)
            sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        else
            sb.AppendFormat("\"{0}\"", EscapeString(value.ToString()));
    }

    static void SerializeObject(IDictionary obj, StringBuilder sb)
    {
        bool first = true;
        sb.Append('{');
        foreach (object e in obj.Keys)
        {
            if (!first) sb.Append(',');
            sb.AppendFormat("\"{0}\":", EscapeString(e.ToString()));
            SerializeValue(obj[e], sb);
            first = false;
        }
        sb.Append('}');
    }

    static void SerializeArray(IList anArray, StringBuilder sb)
    {
        sb.Append('[');
        bool first = true;
        foreach (object obj in anArray)
        {
            if (!first) sb.Append(',');
            SerializeValue(obj, sb);
            first = false;
        }
        sb.Append(']');
    }

    static string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
}