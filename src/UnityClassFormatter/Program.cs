using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityClassFormatter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: UnityClassFormatter <file.cs>");
                return;
            }

            var filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine("File not found.");
                return;
            }

            var code = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var rewriter = new ClassReorderRewriter();
            var newRoot = rewriter.Visit(tree.GetRoot());
            var newCode = newRoot.ToFullString();
            File.WriteAllText(filePath, newCode);
            Console.WriteLine("File reformatted.");
        }
    }

    public class ClassReorderRewriter : CSharpSyntaxRewriter
    {

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var constantFields = new List<MemberInfo>();
            var staticFields = new List<MemberInfo>();
            var serializedFields = new List<MemberInfo>();
            var nonSerializedFields = new List<MemberInfo>();
            var constructors = new List<MemberInfo>();
            var properties = new List<MemberInfo>();
            var eventsDelegates = new List<MemberInfo>();
            var lifeCycleMethods = new List<MemberInfo>();
            var publicMethods = new List<MemberInfo>();
            var privateMethods = new List<MemberInfo>();
            var nestedTypes = new List<MemberInfo>();

            var currentGroupOrder = 0;
            var lastHeaderGroupOrder = -1;

            foreach (var member in node.Members)
            {
                var acc = GetAccessibility(member.Modifiers);
                var accessOrder = GetAccessOrder(acc);
                var name = GetName(member);

                // Detect if the member has a [Header] attribute
                var headerValue = GetHeaderAttribute(member);
                var hasHeader = !string.IsNullOrEmpty(headerValue);

                // If we find a new [Header], increment the group
                if (hasHeader)
                {
                    currentGroupOrder++;
                    lastHeaderGroupOrder = currentGroupOrder;
                }

                // Members without [Header] that come after a group with [Header] belong to that group until another [Header] is found or the type changes
                var memberGroupOrder = (lastHeaderGroupOrder >= 0) ? lastHeaderGroupOrder : currentGroupOrder;

                switch (member)
                {
                    case FieldDeclarationSyntax fd:
                        if (fd.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                            constantFields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        else if (fd.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
                            staticFields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        else
                        {
                            if (fd.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == "SerializeField")))
                                serializedFields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                            else
                                nonSerializedFields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        }
                        break;
                    case PropertyDeclarationSyntax p:
                        properties.Add(new MemberInfo(p, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        break;
                    case ConstructorDeclarationSyntax c:
                        constructors.Add(new MemberInfo(c, accessOrder, name, false, "", 0));
                        break;
                    case MethodDeclarationSyntax m:
                        var methodName = m.Identifier.Text;
                        if (new[] { "Awake", "OnEnable", "OnDisable", "OnDestroy" }.Contains(methodName))
                            lifeCycleMethods.Add(new MemberInfo(m, accessOrder, methodName, false, "", 0));
                        else
                        {
                            if (acc == Accessibility.Public || acc == Accessibility.Internal)
                                publicMethods.Add(new MemberInfo(m, accessOrder, methodName, false, "", 0));
                            else
                                privateMethods.Add(new MemberInfo(m, accessOrder, methodName, false, "", 0));
                        }
                        break;
                    case EventDeclarationSyntax e:
                        eventsDelegates.Add(new MemberInfo(e, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        break;
                    case DelegateDeclarationSyntax d:
                        eventsDelegates.Add(new MemberInfo(d, accessOrder, name, false, "", 0));
                        break;
                    case EventFieldDeclarationSyntax efd:
                        eventsDelegates.Add(new MemberInfo(efd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        break;
                    case ClassDeclarationSyntax cd:
                    case StructDeclarationSyntax sd:
                    case InterfaceDeclarationSyntax id:
                    case EnumDeclarationSyntax ed:
                        nestedTypes.Add(new MemberInfo(member, accessOrder, name, false, "", 0));
                        break;
                    default:
                        nonSerializedFields.Add(new MemberInfo(member, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
                        break;
                }
            }

            // Sort each list: AccessOrder -> GroupOrder -> Name
            constantFields = SortMembers(constantFields);
            staticFields = SortMembers(staticFields);
            // Process serialized fields with header grouping
            serializedFields = ProcessSerializedFields(serializedFields);
            // Non-serialized fields sorted alphabetically
            nonSerializedFields = nonSerializedFields.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
            var fields = serializedFields.Concat(nonSerializedFields).ToList();
            // Move [Header] attributes to the first member in each group
            fields = MoveHeadersToFirst(fields);
            // Add line break after the last [SerializeField] of each group
            fields = AddLineBreakAfterLastSerializedFieldInGroups(fields);
            constructors = constructors.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
            properties = SortMembers(properties);
            eventsDelegates = SortMembers(eventsDelegates);
            lifeCycleMethods = lifeCycleMethods.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
            publicMethods = publicMethods.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
            privateMethods = privateMethods.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
            nestedTypes = nestedTypes.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();

            // Combine
            var newMembers = new SyntaxList<MemberDeclarationSyntax>();
            foreach (var mi in constantFields.Concat(staticFields).Concat(fields).Concat(constructors).Concat(properties).Concat(eventsDelegates).Concat(lifeCycleMethods).Concat(publicMethods).Concat(privateMethods).Concat(nestedTypes))
            {
                newMembers = newMembers.Add(mi.Node);
            }

            return node.WithMembers(newMembers);
        }

        private static Accessibility GetAccessibility(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                return Accessibility.Public;
            if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)) && modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
                return Accessibility.ProtectedAndInternal;
            if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
                return Accessibility.Internal;
            if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
                return Accessibility.Protected;
            if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                return Accessibility.Private;
            return Accessibility.Private;
        }

        private static int GetAccessOrder(Accessibility acc)
        {
            return acc switch
            {
                Accessibility.Public => 0,
                Accessibility.Internal => 1,
                Accessibility.Protected => 2,
                Accessibility.Private => 3,
                Accessibility.ProtectedAndInternal => 1,
                _ => 3,
            };
        }

        // Extract the value of the [Header("...")] attribute
        private static string GetHeaderAttribute(MemberDeclarationSyntax member)
        {
            var attributeLists = member.AttributeLists;
            foreach (var attrList in attributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrName = attr.Name.ToString();
                    if (attrName == "Header" || attrName == "HeaderAttribute")
                    {
                        if (attr.ArgumentList?.Arguments.Count > 0)
                        {
                            var firstArg = attr.ArgumentList.Arguments[0];
                            var expression = firstArg.Expression;
                            if (expression is LiteralExpressionSyntax literal)
                            {
                                return literal.Token.ValueText;
                            }
                        }
                    }
                }
            }
            return "";
        }

        private static string GetName(MemberDeclarationSyntax member)
        {
            return member switch
            {
                FieldDeclarationSyntax fd => fd.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
                PropertyDeclarationSyntax p => p.Identifier.Text,
                ConstructorDeclarationSyntax c => c.Identifier.Text,
                MethodDeclarationSyntax m => m.Identifier.Text,
                EventDeclarationSyntax e => e.Identifier.Text,
                DelegateDeclarationSyntax d => d.Identifier.Text,
                EventFieldDeclarationSyntax efd => efd.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
                ClassDeclarationSyntax cd => cd.Identifier.Text,
                StructDeclarationSyntax sd => sd.Identifier.Text,
                InterfaceDeclarationSyntax id => id.Identifier.Text,
                EnumDeclarationSyntax ed => ed.Identifier.Text,
                _ => "",
            };
        }

        // Special sorting function that maintains groups with [Header]
        private static List<MemberInfo> SortMembers(List<MemberInfo> members)
        {
            return [.. members
              .OrderBy(x => x.AccessOrder)           // 1. By access level
              .ThenBy(x => x.GroupOrder)              // 2. By [Header] group
              .ThenBy(x => x.Name)];                   // 3. Alphabetically by name
        }
        private static List<MemberInfo> ProcessSerializedFields(List<MemberInfo> fields)
        {
            int currentGroup = 0;
            foreach (var mi in fields)
            {
                if (mi.HasHeader)
                {
                    currentGroup++;
                    mi.GroupOrder = currentGroup;
                }
                else
                {
                    mi.GroupOrder = currentGroup;
                }
            }
            return SortMembers(fields);
        }

        private static List<MemberInfo> MoveHeadersToFirst(List<MemberInfo> members)
        {
            var grouped = members.GroupBy(x => x.GroupOrder).OrderBy(g => g.Key);
            var result = new List<MemberInfo>();
            foreach (var group in grouped)
            {
                var groupMembers = group.ToList();
                var withHeader = groupMembers.FirstOrDefault(m => m.HasHeader);
                if (withHeader != null)
                {
                    var headerValue = GetHeaderAttribute(withHeader.Node);
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        var first = groupMembers[0];
                        if (first != withHeader)
                        {
                            var newFirstNode = AddHeaderAttribute(first.Node, headerValue);
                            var newWithHeaderNode = RemoveHeaderAttribute(withHeader.Node);
                            first.Node = newFirstNode;
                            withHeader.Node = newWithHeaderNode;
                            // Update HasHeader
                            first.HasHeader = true;
                            first.HeaderValue = headerValue;
                            withHeader.HasHeader = false;
                            withHeader.HeaderValue = "";
                        }
                    }
                }
                result.AddRange(groupMembers);
            }
            return result;
        }

        private static List<MemberInfo> AddLineBreakAfterLastSerializedFieldInGroups(List<MemberInfo> members)
        {
            // Group by GroupOrder to identify groups
            var grouped = members.GroupBy(x => x.GroupOrder).OrderBy(g => g.Key);
            var result = new List<MemberInfo>();

            foreach (var group in grouped)
            {
                var groupMembers = group.ToList();

                // Find the last [SerializeField] in this group
                MemberInfo? lastSerializedField = null;
                int lastSerializedFieldIndex = -1;

                for (int i = groupMembers.Count - 1; i >= 0; i--)
                {
                    if (groupMembers[i].Node is FieldDeclarationSyntax fd &&
                        fd.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString() == "SerializeField")))
                    {
                        lastSerializedField = groupMembers[i];
                        lastSerializedFieldIndex = i;
                        break;
                    }
                }

                // If there's a last [SerializeField] and it's not the last member in the group
                if (lastSerializedField != null && lastSerializedFieldIndex < groupMembers.Count - 1)
                {
                    // Check if it already has a blank line after it
                    var node = lastSerializedField.Node;
                    var trailingTrivia = node.GetTrailingTrivia();

                    // Count newlines in trailing trivia
                    int newlineCount = trailingTrivia.Count(t => t.IsKind(SyntaxKind.EndOfLineTrivia));

                    // If it doesn't have at least 2 newlines (one for the line end, one for blank line), add one
                    if (newlineCount < 2)
                    {
                        var extraNewline = SyntaxFactory.CarriageReturnLineFeed;
                        var newTrailingTrivia = trailingTrivia.Add(extraNewline);
                        lastSerializedField.Node = node.WithTrailingTrivia(newTrailingTrivia);
                    }
                }

                result.AddRange(groupMembers);
            }

            return result;
        }

        private static List<MemberInfo> AddExtraNewlinesAfterHeaders(List<MemberInfo> members)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].HasHeader)
                {
                    var node = members[i].Node;
                    var trailingTrivia = node.GetTrailingTrivia();
                    var extraNewline = SyntaxFactory.CarriageReturnLineFeed;
                    var newTrailingTrivia = trailingTrivia.Add(extraNewline);
                    members[i].Node = node.WithTrailingTrivia(newTrailingTrivia);
                }
            }
            return members;
        }

        private static MemberDeclarationSyntax AddHeaderAttribute(MemberDeclarationSyntax member, string headerValue)
        {
            var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("Header"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.AttributeArgument(
                            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(headerValue))
                        )
                    )
                )
            );
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                .WithLeadingTrivia(SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace("    ")))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            var newAttributeLists = new[] { attributeList }.Concat(member.AttributeLists).ToList();
            return member.WithAttributeLists(SyntaxFactory.List(newAttributeLists));
        }

        private static MemberDeclarationSyntax RemoveHeaderAttribute(MemberDeclarationSyntax member)
        {
            var newAttributeLists = member.AttributeLists
                .Select(al => al.WithAttributes(SyntaxFactory.SeparatedList(al.Attributes.Where(a => a.Name.ToString() != "Header" && a.Name.ToString() != "HeaderAttribute"))))
                .Where(al => al.Attributes.Any())
                .ToList();
            return member.WithAttributeLists(SyntaxFactory.List(newAttributeLists));
        }

        private class MemberInfo(MemberDeclarationSyntax node, int accessOrder, string name, bool hasHeader, string headerValue, int groupOrder)
        {
            public MemberDeclarationSyntax Node { get; set; } = node;
            public int AccessOrder { get; set; } = accessOrder;
            public string Name { get; set; } = name;
            public bool HasHeader { get; set; } = hasHeader;
            public string HeaderValue { get; set; } = headerValue;
            public int GroupOrder { get; set; } = groupOrder;
        }
    }
}
