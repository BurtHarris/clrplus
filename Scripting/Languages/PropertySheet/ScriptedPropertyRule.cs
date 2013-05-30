namespace ClrPlus.Scripting.Languages.PropertySheet {
    using System;
    using System.Collections.Generic;
    using Core.Exceptions;
    using Core.Extensions;
    using Engine;

    public class ScriptedPropertyRule : PropertyRule {
        private readonly string ScriptType;
        private readonly string ScriptText;
        private Func<string[], object> _scriptDelegate;
        // private string _sourceText;
        private object _result;

        public bool Executed {get; set;}
        public bool Compiled {get; set;}

        public void Execute(string[] args) {
            if (!Compiled) {
                Compile();
            }
            _result = _scriptDelegate(args);
        }

        public void ExecuteIfNeccessary() {
            if (!Executed) {
                Execute(null);
            }
        }

        public void Compile() {
            lock (this) {
                if (Compiled) {
                    Compiled = false;
                    _scriptDelegate = UniversalEngine.Compile(ScriptType, ScriptText);
                }
                if (_scriptDelegate != null) {
                    Compiled = true;
                } else {
                    throw new ClrPlusException("Unable to compile {0} script into delgate ".format(ScriptType));
                }
            }
        }

        internal ScriptedPropertyRule(Rule parent, string name, string scriptType, string scriptText, string sourceText) : base(parent, name) {
            ScriptType = scriptType;
            ScriptText = scriptText;
        }

        internal override string SourceString {
            get {
                // return _sourceText;
                return string.Empty;
            }
        }

        public override IEnumerable<PropertyValue> PropertyValues {
            get {
                ExecuteIfNeccessary();
                throw new NotImplementedException();
            }
        }

        public override string Value {
            get {
                // ExecuteIfNeccessary();
                throw new NotImplementedException();
            } set {
                throw new NotImplementedException();
            }
        }

        public override IEnumerable<string> Values {
            get {
                throw new NotImplementedException();
            }
        }

        public override IEnumerable<string> Labels {
            get {
                throw new NotImplementedException();
            }
        }

        public override bool HasValues {
            get {
                throw new NotImplementedException();
            }
        }

        public override bool HasValue {
            get {
                throw new NotImplementedException();
            }
        }

        public override IPropertyValue this[string label] {
            get {
                throw new NotImplementedException();
            }
        }

        public override PropertyValue GetPropertyValue(string label, IEnumerable<string> collections = null) {
            throw new NotImplementedException();
        }
    }
}