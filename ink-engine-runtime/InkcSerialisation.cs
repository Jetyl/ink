﻿using System.Text;
using System.Collections.Generic;

namespace Ink.Runtime
{
    // Pseudo bytecode. Text based format that is ascii-character driven.
    // At the fundamental runtime-object level, the parsing can branch based on
    // the initial character. Full dictionary of them:
    //
    // {   - Container
    // [   - Container's named content section
    // >   - Divert
    // ^   - Divert target value
    // .   - Native function
    // "   - String value
    // \n  - Single newline string value
    // =   - Variable assignment
    // $   - Variable reference
    // R   - Variable pointer value (R for reference)
    // G   - Glue
    // -, 0-9 - Int or float
    // (   - Begin evaluation mode control command
    // )   - End evaluation mode control command
    // «   - Begin string control command
    // »   - End string control command
    // #   - Other control commands
    // *   - Choice point
    // V   - Void

    internal class InkcWriter
    {
        public InkcWriter(Story story)
        { 
            _story = story;
            _sb = new StringBuilder ();
        }
        
        public override string ToString()
        {
            if (_finalString == null) {
                int version = Story.inkVersionCurrent;
                var rootContainer = _story.mainContentContainer;

                Write ("inkc ");
                Write (version);
                Write ("\n");
                Write (rootContainer);

                _finalString = _sb.ToString ();
            }

            return _finalString;
        }

        void Write(string str)
        {
            _sb.Append (str);
        }

        // Write out the string, but escape " and \ with \
        void WriteQuotedString(string str)
        {
            Write ("\"");

            foreach (char c in str) {
                if (c == '"' || c == '\\')
                    _sb.Append ('\\');
                _sb.Append (c);
            }

            Write ("\"");
        }

        void Write(int value)
        {
            _sb.Append (value);
            _sb.Append (" ");
        }

        void Write(float value)
        {
            // Force decimal point
            _sb.Append (value.ToString(".0#############"));
            _sb.Append (" ");
        }

        void Write(Divert divert)
        {
            Write (">");

            if (divert.hasVariableTarget) {
                Write ("?");
                Write (divert.variableDivertName);
            } else {
                Write (divert.targetPathString);
            }
            Write (" ");

            if (divert.isConditional)
                Write ("c");

            if (divert.isExternal) {
                Write ("x");
                Write (divert.externalArgs);
            }

            if (divert.pushesToStack) {
                if (divert.stackPushType == PushPopType.Function)
                    Write ("f");
                else if (divert.stackPushType == PushPopType.Tunnel)
                    Write ("t");
            }

            Write (">");
        }

        void Write(DivertTargetValue divertTargetValue)
        {
            Write ("^");
            Write (divertTargetValue.targetPath.componentsString);
            Write (" ");
        }

        void Write(VariableAssignment varAss)
        {
            Write ("=");
            if (!varAss.isGlobal)
                Write ("t"); // t=temp
            if (!varAss.isNewDeclaration)
                Write ("r"); // r=reassignment
            Write(" ");
            Write (varAss.variableName);
            Write (" ");
        }

        void Write(VariableReference varRef)
        {
            Write ("$");

            var readCountPath = varRef.pathStringForCount;
            if (readCountPath != null) {
                Write ("&");
                Write (readCountPath);
            } else {
                Write (varRef.name);
            }

            Write (" ");
        }

        void Write(VariablePointerValue varPtr)
        {
            // R for ref (ink keyword)
            Write ("R");
            Write (varPtr.contextIndex);
            Write (varPtr.value);
            Write (" ");
        }

        void Write(ChoicePoint choicePoint)
        {
            Write ("*");

            int flags = choicePoint.flags;
            if (flags > 0)
                Write (flags);
            else
                Write (" ");

            Write (choicePoint.pathStringOnChoice);
            Write (" ");
        }
            
        void Write(Container container)
        {
            Write ("{");

            if (container.hasValidName) {
                Write ("'" + container.name + "'");
            }

            Write (container.content);

            var namedOnlyContent = container.namedOnlyContent;
            if (namedOnlyContent != null && namedOnlyContent.Count > 0) {
                Write ("[");

                foreach (var namedRuntimeObj in container.namedOnlyContent)
                    Write (namedRuntimeObj.Value);

                Write ("]");
            }

            if (container.countFlags != 0) {
                Write ("f");
                Write (container.countFlags);
            }
                
            Write("}");
        }

        void Write(List<Runtime.Object> runtimeObjList)
        {
            foreach (var obj in runtimeObjList) {
                Write (obj);
            }
        }
            
        void Write(Runtime.Object runtimeObj)
        {
            if (runtimeObj is Container) {
                Write ((Container)runtimeObj);
            } else if (runtimeObj is StringValue) {

                var strVal = (StringValue)runtimeObj;

                if (strVal.isNewline)
                    Write ("\n");
                else {
                    WriteQuotedString (strVal.value);
                }

            } else if (runtimeObj is Glue) {
                Write ("G");
                var glue = (Glue)runtimeObj;
                if (glue.isBi)
                    Write ("b");
                else if (glue.isRight)
                    Write (">");
                else
                    Write ("<");
            } else if (runtimeObj is ControlCommand) {
                var controlCommand = (ControlCommand)runtimeObj;
                switch (controlCommand.commandType) {
                case ControlCommand.CommandType.EvalStart:
                    Write ("(");
                    break;
                case ControlCommand.CommandType.EvalEnd:
                    Write (")");
                    break;
                case ControlCommand.CommandType.BeginString:
                    Write ("«");
                    break;
                case ControlCommand.CommandType.EndString:
                    Write ("»");
                    break;
                default:
                    Write ("#");
                    Write (InkcControlCommand.GetName (controlCommand));
                    break;
                }
            } else if (runtimeObj is NativeFunctionCall) {
                var call = (NativeFunctionCall)runtimeObj;
                Write ("." + call.name + " ");
            } else if (runtimeObj is IntValue) {
                Write (((IntValue)runtimeObj).value);
            } else if (runtimeObj is FloatValue) {
                Write (((FloatValue)runtimeObj).value);
            } else if (runtimeObj is Divert) {
                Write ((Divert)runtimeObj);
            } else if (runtimeObj is DivertTargetValue) {
                Write ((DivertTargetValue)runtimeObj);
            } else if (runtimeObj is VariableAssignment) {
                Write ((VariableAssignment)runtimeObj);
            } else if (runtimeObj is VariableReference) {
                Write ((VariableReference)runtimeObj);
            } else if (runtimeObj is VariablePointerValue) {
                Write ((VariablePointerValue)runtimeObj);
            } else if (runtimeObj is ChoicePoint) {
                Write ((ChoicePoint)runtimeObj);
            } else if (runtimeObj is Void) {
                Write ("V");
            }

            else {
                throw new System.NotImplementedException (runtimeObj.GetType().Name + " not yet implemented");
            }
        }

        Story _story;
        StringBuilder _sb;
        string _finalString;
    }

    internal class InkcReader
    {
        public InkcReader(string str)
        {
            _str = str;
            _index = 0;
        }

        public int ReadHeaderWithVersion()
        {
            Require( ReadString ("inkc "), "Not valid inkc - no 'inkc' header tag");

            var version = (int)ReadNumberValue();

            ReadString ("\n");

            return version;
        }

        void Require(bool b, string errorMessage=null)
        {
            if (b == false) Error (errorMessage);
        }

        void Require(object val, string errorMessage=null)
        {
            if (val == null) Error (errorMessage);
        }

        void Error(string err = null)
        {
            if (err == null)
                err = "Error in inkc format";
            throw new System.Exception (err);
        }

        // Full syntax:
        // {'containerName'runtimeobjects[namedonlycontent]}
        public Container ReadContainer()
        {
            Require (ReadString ("{"));

            var c = new Container ();

            if (ReadString ("'")) {
                c.name = ReadUntil ('\'');
            }

            while( !ReadString("}") ) {
                var obj = ReadRuntimeObject ();
                c.AddContent (obj);

                // Optional named content
                if (ReadString ("[")) {
                    while( !ReadString("]") ) {
                        var namedObj = ReadRuntimeObject ();
                        c.AddToNamedContentOnly ((INamedContent)namedObj);
                    }
                }

                if (ReadString ("f"))
                    c.countFlags = (int) ReadNumberValue ();
            }
                
            return c;
        }

        Divert ReadDivert()
        {
            Require(ReadString (">"));

            var divert = new Divert ();

            if (ReadString ("?"))
                divert.variableDivertName = ReadUntil (' ');
            else
                divert.targetPathString = ReadUntil (' ');

            divert.isConditional = ReadString("c");

            if (ReadString ("x")) {
                divert.isExternal = true;
                divert.externalArgs = (int)ReadNumberValue ();
            }

            if (ReadString ("f")) {
                divert.pushesToStack = true;
                divert.stackPushType = PushPopType.Function;
            } else if (ReadString ("t")) {
                divert.pushesToStack = true;
                divert.stackPushType = PushPopType.Tunnel;
            }

            Require (ReadString (">"));

            return divert;
        }

        DivertTargetValue ReadDivertTargetValue()
        {
            Require (ReadString ("^"));
            var pathStr = ReadUntil (' ');
            return new DivertTargetValue (new Path (pathStr));
        }

        VariableAssignment ReadVariableAssignment()
        {
            Require (ReadString ("="));

            bool isGlobal = !ReadString ("t"); // t=temp
            bool isNewDeclaration = !ReadString ("r"); // r=reassignment
                
            ReadString (" ");
            string variableName = ReadUntil (' ');

            var varAss = new VariableAssignment (variableName, isNewDeclaration);
            varAss.isGlobal = isGlobal;
            return varAss;
        }

        VariableReference ReadVariableReference()
        {
            Require(ReadString ("$"));
            bool isReadCount = ReadString ("&");
            string name = ReadUntil (' ');

            var varRef = new VariableReference ();

            if (isReadCount)
                varRef.pathStringForCount = name;
            else
                varRef.name = name;
            
            return varRef;
        }

        VariablePointerValue ReadVariablePointerValue()
        {
            // R for reference (since it's used for "ref" keyword in ink)
            Require (ReadString ("R"));

            int context = (int) ReadNumberValue ();

            string variableName = ReadUntil (' ');

            return new VariablePointerValue (variableName, context);
        }

        ChoicePoint ReadChoicePoint()
        {
            Require (ReadString ("*"));

            var choicePoint = new ChoicePoint ();

            var flagsStr = ReadUntil (' ');
            if (flagsStr.Length > 0)
                choicePoint.flags = int.Parse (flagsStr);
            else
                choicePoint.flags = 0;

            choicePoint.pathStringOnChoice = ReadUntil (' ');

            return choicePoint;
        }

        Runtime.Object ReadRuntimeObject()
        {
            char peekedChar = _str [_index];

            if ( (peekedChar >= '0' && peekedChar <= '9') || peekedChar == '-' ) {
                return Value.Create(ReadNumberValue ());
            }

            switch (peekedChar) {
            case '\n':
                ReadString ("\n");
                return new StringValue ("\n");

            case '"':
                var str = new StringValue (ReadQuotedString());
                return str;

            // More readable syntax than #ev and #/e
            case '(':
                ReadString ("(");
                return ControlCommand.EvalStart ();
            case ')':
                ReadString (")");
                return ControlCommand.EvalEnd ();
            case '«':
                ReadString ("«");
                return ControlCommand.BeginString ();
            case '»':
                ReadString ("»");
                return ControlCommand.EndString ();

            case '#':
                ReadString ("#");
                return InkcControlCommand.WithName (ReadString (InkcControlCommand.NameLength));

            case '{':
                return ReadContainer ();

            case 'G':
                ReadString ("G");
                var glueTypeChar = ReadString (1);
                if (glueTypeChar == "b") return new Glue (GlueType.Bidirectional);
                else if( glueTypeChar == "<") return new Glue (GlueType.Left);
                else return new Glue (GlueType.Right);


            // Operation
            case '.':
                ReadString (".");
                var opName = ReadUntil (' ');
                return NativeFunctionCall.CallWithName (opName);

            // Divert
            case '>':
                return ReadDivert ();

            // Divert target value
            case '^':
                return ReadDivertTargetValue ();

            // Variable assignment
            case '=':
                return ReadVariableAssignment ();

            // Variable reference
            case '$':
                return ReadVariableReference ();

            // Variable pointer value
            case 'R':
                return ReadVariablePointerValue ();

            // Choice point
            case '*':
                return ReadChoicePoint ();

            // Void
            case 'V':
                ReadString ("V");
                return new Void ();
            }
                
            return null;
        }

        object ReadNumberValue()
        {
            var numStr = ReadUntil (' ');

            int intVal;
            if (int.TryParse (numStr, out intVal))
                return intVal;
            else
                return float.Parse (numStr);
        }


        bool ReadString(string str)
        {
            if (_str.Length < _index + str.Length)
                return false;

            if (_str.Substring (_index, str.Length) == str) {
                _index += str.Length;
                return true;
            }

            return false;
        }

        string ReadString(int length)
        {
            if (_index + length > _str.Length)
                return null;

            var str = _str.Substring (_index, length);

            _index += length;

            return str;
        }

        string ReadQuotedString()
        {
            var sb = new StringBuilder ();

            Require(ReadString("\""));

            // Skip over the character that follows a \ since
            // it's escaped.
            while (_index < _str.Length-1) {
                char c = _str [_index];
                if (c == '\\')
                    _index++;
                else if (c == '"')
                    break;

                sb.Append (_str [_index]);
                _index++;
            }

            Require(ReadString("\""));

            return sb.ToString ();
        }

        string ReadUntil(char c)
        {
            var charPos = _str.IndexOf (c, _index);
            if (charPos == -1)
                return null;

            string foundStr = _str.Substring (_index, charPos - _index);

            // Step over terminator
            _index = charPos+1;

            return foundStr;
        }

        string _str;
        int _index;
    }

    static internal class InkcControlCommand
    {
        public const int NameLength = 2;

        public static ControlCommand WithName(string name)
        {
            SetupNamesIfNecessary ();

            ControlCommand.CommandType type;
            if (!_controlCommandTypes.TryGetValue (name, out type))
                return null;

            return new ControlCommand (type);
        }

        public static string GetName(ControlCommand command)
        {
            SetupNamesIfNecessary ();

            return _controlCommandNames [(int)command.commandType];
        }

        static void SetupNamesIfNecessary()
        {
            if (_controlCommandNames != null)
                return;
            
            _controlCommandNames = new string[(int)ControlCommand.CommandType.TOTAL_VALUES];
            _controlCommandTypes = new Dictionary<string, ControlCommand.CommandType> ();

            // These four are replaced with slightly more readable single characters handled
            // as special cases: ( ) « »
            _controlCommandNames [(int)ControlCommand.CommandType.EvalStart] = "ev";     // (
            _controlCommandNames [(int)ControlCommand.CommandType.EvalEnd] = "/e";       // )
            _controlCommandNames [(int)ControlCommand.CommandType.BeginString] = "st";   // «
            _controlCommandNames [(int)ControlCommand.CommandType.EndString] = "/s";     // »


            _controlCommandNames [(int)ControlCommand.CommandType.EvalOutput] = "ou";
            _controlCommandNames [(int)ControlCommand.CommandType.Duplicate] = "du";
            _controlCommandNames [(int)ControlCommand.CommandType.PopEvaluatedValue] = "po";
            _controlCommandNames [(int)ControlCommand.CommandType.PopFunction] = "rt";
            _controlCommandNames [(int)ControlCommand.CommandType.PopTunnel] = ">>";
            _controlCommandNames [(int)ControlCommand.CommandType.NoOp] = "no";
            _controlCommandNames [(int)ControlCommand.CommandType.ChoiceCount] = "cc";
            _controlCommandNames [(int)ControlCommand.CommandType.TurnsSince] = "tu";
            _controlCommandNames [(int)ControlCommand.CommandType.VisitIndex] = "vi";
            _controlCommandNames [(int)ControlCommand.CommandType.SequenceShuffleIndex] = "se";
            _controlCommandNames [(int)ControlCommand.CommandType.StartThread] = "th";
            _controlCommandNames [(int)ControlCommand.CommandType.Done] = "dn";
            _controlCommandNames [(int)ControlCommand.CommandType.End] = "en";

            for (int i = 0; i < (int)ControlCommand.CommandType.TOTAL_VALUES; ++i) {
                string name = _controlCommandNames [i];
                if (name == null)
                    throw new System.Exception ("Control command not accounted for in serialisation");
                else {
                    _controlCommandTypes [name] = (ControlCommand.CommandType)i;
                }  
            }
        }

        static string[] _controlCommandNames;
        static Dictionary<string, ControlCommand.CommandType> _controlCommandTypes;
    }
}

