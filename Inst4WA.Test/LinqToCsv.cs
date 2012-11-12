#region Copyright Notice
/*
Copyright © Microsoft Open Technologies, Inc.
All Rights Reserved
Apache 2.0 License

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

[Serializable]
public class CsvParseException : Exception
{
    public CsvParseException(string message)
        : base(message)
    {
    }
}

public static class MyExtensions
{
    private enum State
    {
        AtBeginningOfToken,
        InNonQuotedToken,
        InQuotedToken,
        ExpectingComma,
        InEscapedCharacter
    };

    public static string[] CsvSplit(this String source)
    {
        List<string> splitString = new List<string>();
        List<int> slashesToRemove = null;
        State state = State.AtBeginningOfToken;
        char[] sourceCharArray = source.ToCharArray();
        int tokenStart = 0;
        int len = sourceCharArray.Length;
        for (int i = 0; i < len; ++i)
        {
            switch (state)
            {
                case State.AtBeginningOfToken:
                    if (sourceCharArray[i] == '"')
                    {
                        state = State.InQuotedToken;
                        slashesToRemove = new List<int>();
                        continue;
                    }
                    if (sourceCharArray[i] == ',')
                    {
                        splitString.Add("");
                        tokenStart = i + 1;
                        continue;
                    }
                    state = State.InNonQuotedToken;
                    continue;
                case State.InNonQuotedToken:
                    if (sourceCharArray[i] == ',')
                    {
                        splitString.Add(
                            source.Substring(tokenStart, i - tokenStart));
                        state = State.AtBeginningOfToken;
                        tokenStart = i + 1;
                    }
                    continue;
                case State.InQuotedToken:
                    if (sourceCharArray[i] == '"')
                    {
                        state = State.ExpectingComma;
                        continue;
                    }
                    if (sourceCharArray[i] == '\\')
                    {
                        state = State.InEscapedCharacter;
                        slashesToRemove.Add(i - tokenStart);
                        continue;
                    }
                    continue;
                case State.ExpectingComma:
                    if (sourceCharArray[i] != ',')
                        throw new CsvParseException("Expecting comma");
                    string stringWithSlashes =
                        source.Substring(tokenStart, i - tokenStart);
                    foreach (int item in slashesToRemove.Reverse<int>())
                        stringWithSlashes =
                            stringWithSlashes.Remove(item, 1);
                    splitString.Add(
                        stringWithSlashes.Substring(1,
                            stringWithSlashes.Length - 2));
                    state = State.AtBeginningOfToken;
                    tokenStart = i + 1;
                    continue;
                case State.InEscapedCharacter:
                    state = State.InQuotedToken;
                    continue;
            }
        }
        switch (state)
        {
            case State.AtBeginningOfToken:
                splitString.Add("");
                return splitString.ToArray();
            case State.InNonQuotedToken:
                splitString.Add(
                    source.Substring(tokenStart,
                        source.Length - tokenStart));
                return splitString.ToArray();
            case State.InQuotedToken:
                throw new CsvParseException("Expecting ending quote");
            case State.ExpectingComma:
                string stringWithSlashes =
                    source.Substring(tokenStart, source.Length - tokenStart);
                foreach (int item in slashesToRemove.Reverse<int>())
                    stringWithSlashes = stringWithSlashes.Remove(item, 1);
                splitString.Add(
                    stringWithSlashes.Substring(1,
                        stringWithSlashes.Length - 2));
                return splitString.ToArray();
            case State.InEscapedCharacter:
                throw new CsvParseException("Expecting escaped character");
        }
        throw new CsvParseException("Unexpected error");
    }
}
