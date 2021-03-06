using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pocket
{
    [DebuggerStepThrough]
    internal class Formatter
    {
        private readonly string template;

        private static readonly Regex tokenRegex = new Regex(
            @"{(?<key>[^{}:]*)(?:\:(?<format>.+))?}",
            RegexOptions.IgnoreCase |
            RegexOptions.Multiline |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled
        );

        private readonly List<Action<StringBuilder, object>> argumentFormatters = new List<Action<StringBuilder, object>>();

        private readonly List<string> tokens = new List<string>();

        public Formatter(string template)
        {
            this.template = template;
            var matches = tokenRegex.Matches(template);

            foreach (Match match in matches)
            {
                var argName = match.Groups["key"].Captures[0].Value;
                tokens.Add(argName);

                var replacementTarget = match.Value;

                var formatStr = match.Groups["format"].Success
                                    ? match.Groups["format"].Captures[0].Value
                                    : null;

                void format(StringBuilder sb, object value)
                {
                    string formattedParam = null;

                    if (!string.IsNullOrEmpty(formatStr))
                    {
                        var formattableParamValue = value as IFormattable;
                        if (formattableParamValue != null)
                        {
                            formattedParam = formattableParamValue.ToString(formatStr, CultureInfo.CurrentCulture);
                        }
                    }

                    if (formattedParam == null)
                    {
                        formattedParam = value.ToLogString();
                    }

                    sb.Replace(replacementTarget, formattedParam);
                }

                argumentFormatters.Add(format);
            }
        }

        public IReadOnlyList<string> Tokens => tokens;

        public FormatterResult Format(
            IReadOnlyList<object> args,
            IList<(string Name, object Value)> knownProperties)
        {
            if (args == null)
            {
                args = new object[argumentFormatters.Count];
            }

            var stringBuilder = new StringBuilder(template);
            var result = new FormatterResult(stringBuilder);

            for (var i = 0; i < Math.Min(argumentFormatters.Count, args.Count); i++)
            {
                var argument = args[i];
                argumentFormatters[i](stringBuilder, argument);
                result.Add(Tokens[i], argument);
            }

            if (args.Count > argumentFormatters.Count || knownProperties?.Count > 0)
            {
                stringBuilder.Append(" +[ ");
                var first = true;

                if (args.Count > 0)
                {
                    for (var i = argumentFormatters.Count; i < args.Count; i++)
                    {
                        var argument = args[i];
                        if (!first)
                        {
                            stringBuilder.Append(", ");
                        }
                        first = false;
                        stringBuilder.Append(argument.ToLogString());
                        result.Add($"arg{i}", argument);
                    }
                }

                if (knownProperties?.Count > 0)
                {
                    for (var i = 0; i < knownProperties.Count; i++)
                    {
                        var property = knownProperties[i];
                        if (!first)
                        {
                            stringBuilder.Append(", ");
                        }
                        first = false;
                        stringBuilder.Append(property.ToLogString());
                    }
                }

                stringBuilder.Append(" ]");
            }

            return result;
        }

        public FormatterResult Format(params object[] args) => Format(args, null);

        internal class FormatterResult : IReadOnlyList<(string Name, object Value)>
        {
            private readonly StringBuilder formattedMessage;

            public FormatterResult(StringBuilder formattedMessage)
            {
                this.formattedMessage = formattedMessage;
            }

            private readonly List<(string Name, object Value)> properties = new List<(string, object )>();

            public void Add(string key, object value)
            {
                properties.Add((key, value));
            }

            public IEnumerator<(string Name, object Value)> GetEnumerator()
            {
                return properties.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int Count => properties.Count;

            public (string Name, object Value) this[int index] => properties[index];

            public override string ToString()
            {
                return formattedMessage.ToString();
            }
        }

        private static readonly ConcurrentDictionary<string, Formatter> formatters = new ConcurrentDictionary<string, Formatter>();

        public static Formatter Parse(string template)
        {
            return formatters.GetOrAdd(template, t => new Formatter(t));
        }
    }
}
