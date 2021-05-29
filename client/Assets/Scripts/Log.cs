using System;
using UnityEngine;


static class Log
{
    public static void Info(string str)
    {
        Debug.Log(str);
    }
    public static void InfoFormat(string str, params object[] args)
    {
        Debug.LogFormat(str, args);
    }
}
