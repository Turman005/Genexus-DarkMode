// Xpz.Dark - utilitarios de tipo: "este controle e de tal biblioteca?".

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Xpz.Dark
{
    internal static class Familia
    {
        /// <summary>O tipo (ou um ancestral) tem este nome completo?</summary>
        public static bool Deriva(Type t, string nomeCompleto)
        {
            for (Type b = t; b != null; b = b.BaseType)
                if (b.FullName == nomeCompleto) return true;
            return false;
        }

        public static bool Igual(Color a, Color b) { return a.IsEmpty == b.IsEmpty && a.ToArgb() == b.ToArgb(); }

        public static bool DerivaDePrefixo(Type t, string prefixo)
        {
            for (Type b = t; b != null; b = b.BaseType)
                if ((b.FullName ?? "").StartsWith(prefixo, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
