//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Scripting.Languages.PropertySheet {
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Core.Extensions;

    public class PropertySheet : DynamicObject {
        public string Filename {get; internal set;}
        public string ImportedAsFilename {get; internal set;}

        private static readonly Regex Macro = new Regex(@"(\$\{(.*?)\})");
        private readonly List<Rule> _rules = new List<Rule>();
        private readonly List<PropertySheet> _importedSheets = new List<PropertySheet>();
        internal PropertySheet UserOverride;

        public delegate IEnumerable<object> GetCollectionDelegate(string collectionName);

        public StringExtensions.GetMacroValueDelegate GetMacroValue;
        public StringExtensions.GetMacroValueDelegate PreprocessProperty;
        public StringExtensions.GetMacroValueDelegate PostprocessProperty;

        public GetCollectionDelegate GetCollection;

        public void AddImportedSheet(PropertySheet importedSheet) {
            _importedSheets.Insert(0, importedSheet);
        }

        public IEnumerable<Rule> Rules {
            get {
                return UserOverride == null ? _rules.Union(_importedSheets.SelectMany(each => each.Rules)) :
                    UserOverride.Rules.Union(_rules).Union(_importedSheets.SelectMany(each => each.Rules));
            }
        }

        public virtual IEnumerable<string> FullSelectors {
            get {
                return Rules.Select(each => each.FullSelector);
            }
        }

        public Rule AddRule(string name = "*", string parameter = null, string @class = null, string id = null) {
            Rule result;
            _rules.Insert(0, result = new Rule(this) {
                Name = name,
                Parameter = parameter,
                Class = @class,
                Id = id,
            });
            return result;
        }

        public void RemoveRule(Rule rule) {
            _rules.Remove(rule);
        }

        public Rule this[string name = "*", string parameter = null, string @class = null, string id = null] {
            get {
                return SelectRules(name, parameter, @class, id).FirstOrDefault() ?? AddRule(name, parameter, @class, id);
            }
        }

        public IEnumerable<Rule> SelectRules(string name = "*", string parameter = null, string @class = null, string id = null) {
            return from rule in Rules
                where rule.Name == name &&
                    parameter.EqualsEx(rule.Parameter) &&
                    @class.EqualsEx(rule.Class) &&
                    id.EqualsEx(rule.Id)
                select rule;
        }

        public static PropertySheet Parse(string text, string originalFilename) {
            var result = PropertySheetParser.Parse(text, originalFilename);
            result.Filename = originalFilename;
            return result;
        }

        public static PropertySheet Load(string path) {
            return Parse(File.ReadAllText(path), path);
        }

        public virtual void Save(string path) {
            File.WriteAllText(path, ToString());
        }

        internal string ResolveMacros(string value, object[] eachItems = null) {
            if (PreprocessProperty != null) {
                foreach (StringExtensions.GetMacroValueDelegate preprocess in PreprocessProperty.GetInvocationList()) {
                    value = preprocess(value);
                }
            }

            if (GetMacroValue != null) {
                value = ProcessMacroInternal(value, eachItems);
            }

            if (PostprocessProperty != null) {
                foreach (StringExtensions.GetMacroValueDelegate postprocess in PostprocessProperty.GetInvocationList()) {
                    value = postprocess(value);
                }
            }

            return value;
        }

        private string ProcessMacroInternal(string value, object[] eachItems) {
            bool keepGoing;
            if (value == null) {
                return null;
            }

            do {
                keepGoing = false;

                var matches = Macro.Matches(value);
                foreach (var m in matches) {
                    var match = m as Match;
                    var innerMacro = match.Groups[2].Value;
                    var outerMacro = match.Groups[1].Value;
                    // var replacement = GetMacroValue(innerMacro);
                    string replacement = null;

                    // get the first responder.
                    foreach (StringExtensions.GetMacroValueDelegate del in GetMacroValue.GetInvocationList()) {
                        replacement = del(innerMacro);
                        if (replacement != null) {
                            break;
                        }
                    }

                    if (!eachItems.IsNullOrEmpty()) {
                        // try resolving it as an ${each.property} style.
                        // the element at the front is the 'this' value
                        // just trim off whatever is at the front up to and including the first dot.
                        try {
                            var ndx = GetIndex(innerMacro);

                            if (ndx >= 0) {
                                if (ndx < eachItems.Length) {
                                    value = value.Replace(outerMacro, eachItems[ndx].ToString());
                                    keepGoing = true;
                                }
                            } else {
                                if (innerMacro.Contains(".")) {
                                    var indexOfDot = innerMacro.IndexOf('.');
                                    ndx = GetIndex(innerMacro.Substring(0, indexOfDot));
                                    if (ndx >= 0) {
                                        if (ndx < eachItems.Length) {
                                            innerMacro = innerMacro.Substring(indexOfDot + 1).Trim();

                                            var v = eachItems[ndx].SimpleEval(innerMacro);
                                            if (v != null) {
                                                var r = v.ToString();
                                                value = value.Replace(outerMacro, r);
                                                keepGoing = true;
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {
                            // meh. screw em'
                        }
                    }

                    if (replacement != null) {
                        value = value.Replace(outerMacro, replacement);
                        keepGoing = true;
                        break;
                    }
                }
            } while (keepGoing);
            return value;
        }

        private int GetIndex(string innerMacro) {
            int ndx;
            if (!Int32.TryParse(innerMacro, out ndx)) {
                return innerMacro.Equals("each", StringComparison.CurrentCultureIgnoreCase) ? 0 : -1;
            }
            return ndx;
        }

        public override string ToString() {
            var imports = (from sheet in _importedSheets where sheet.ImportedAsFilename != null select ImportedAsFilename).Distinct().Aggregate("", (current, each) => string.IsNullOrEmpty(current) ? "" : current + "@import {0};\r\n".format(QuoteIfNeeded(each)));
            return (_rules as IEnumerable<Rule>).Reverse().Aggregate(imports, (current, each) => current + each.SourceString);
        }

        public bool PreferDashedNames {get; set;}

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            // we have to also potentially translate fooBar into foo-bar so we can test for 
            // dashed-names properly.
            var alternateName = binder.Name.CamelCaseToDashed();

            var val = (from rule in Rules where rule.Name == binder.Name || rule.Name == alternateName select rule).ToArray();

            switch (val.Length) {
                case 0:
                    result = this[PreferDashedNames ? alternateName : binder.Name]; // we'll implicity add one by this name.
                    break;
                case 1:
                    result = val[0]; // will return the single item *as* a single item.
                    break;
                default:
                    result = val; // will return the collection instead 
                    break;
            }
            return true;
        }

        internal static string QuoteIfNeeded(string val) {
            if (val == null) {
                return "<null>";
            }

            if (val.IsNullOrEmpty()) {
                return @"""""";
            }

            if (val.OnlyContains(StringExtensions.LettersNumbersUnderscoresAndDashesAndDots) && StringExtensions.Letters.Contains(val[0]))
            {
                return val;
            }

            return val.Contains("\r") || val.Contains("\n") || val.Contains("=") || val.Contains("\t")
                ? @"@""{0}""".format(val)
                : @"""{0}""".format(val);
        }
    }
}