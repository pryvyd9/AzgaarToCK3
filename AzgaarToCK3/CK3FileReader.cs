using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AzgaarToCK3;

//public static class CK3FileReader
//{
//    public enum TokenType
//    {
//        unknown,
//        fieldName,
//        fieldValue,

//        literal,
//        objectStart,
//        objectEnd,
//    }

//    public record IncompleteToken(TokenType type, string? value);


//    public class CompleteToken
//    {
//        Dictionary<string, CompleteToken> ObjectValues { get; set; }
//        CompleteToken[] ArrayValues { get; set; }
//        string Value { get; set; }
//    }


//    public static Dictionary<string, object> Read(string filename)
//    {
//        try
//        {
//            var content = File.ReadAllText(filename);

//            //var tokens = new List<string>();

//            Stack<IncompleteToken> incompleteTokens = new();
//            Stack<string> completeTokens = new();

//            //string currentToken;

//            //var leftContent = content.AsSpan();
//            int cursor = 0;
//            TokenType expectedToken = TokenType.fieldName;

//            var whiteSpaceRegex = new Regex(@"\s", RegexOptions.Compiled);

//            var tokenBeginningRegex = new Regex(@"\s*(.)", RegexOptions.Compiled);

//            do
//            {
//                var leftContent = content[cursor..];
//                if (expectedToken is TokenType.fieldName)
//                {
//                    var token = new string(leftContent.TakeWhile(n => n is not '=').ToArray());
//                    cursor += token.Length;
//                    // jump over '='
//                    cursor += 1;

//                    AddIncompleteToken(TokenType.fieldName, token.Trim());
//                    expectedToken = TokenType.fieldValue;
//                }
//                else if (expectedToken is TokenType.fieldValue)
//                {
//                    var tokenBeginning = tokenBeginningRegex.Match(leftContent).Groups[0].Value;
//                    if (tokenBeginning is "{")
//                    {
//                        AddIncompleteToken(TokenType.objectStart, null);

//                    }
//                }


//            } while (cursor < content.Length);

//            return null;

//            void AddIncompleteToken(TokenType type, string? value) => incompleteTokens.Push(new IncompleteToken(type, value));
//        }
//        catch (Exception ex)
//        {
//            Debugger.Break();
//            throw;
//        }
//    }
//}
