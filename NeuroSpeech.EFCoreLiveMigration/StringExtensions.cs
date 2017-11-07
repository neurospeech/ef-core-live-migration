using System;
using System.Collections.Generic;
using System.Text;

namespace NeuroSpeech.EFCoreLiveMigration
{
    public static class StringExtensions
    {

        public static bool ContainsIgnoreCase(this string text, string test) {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (string.IsNullOrWhiteSpace(test))
                return false;
            return text.IndexOf(test, StringComparison.OrdinalIgnoreCase) != -1;
        }

        public static bool EqualsIgnoreCase(this string text, string test) {
            if (string.IsNullOrWhiteSpace(text))
                return string.IsNullOrWhiteSpace(test);
            if (string.IsNullOrWhiteSpace(test))
                return false;
            return text.Equals(test, StringComparison.OrdinalIgnoreCase);
        }

    }
}
