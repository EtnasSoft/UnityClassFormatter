using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class Program {
  static void Main(string[] args) {
    if (args.Length != 1) {
      Console.WriteLine("Usage: ClassFormatter <file.cs>");
      return;
    }

    string filePath = args[0];
    if (!File.Exists(filePath)) {
      Console.WriteLine("File not found.");
      return;
    }

    string code = File.ReadAllText(filePath);
    SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
    var rewriter = new ClassReorderRewriter();
    SyntaxNode newRoot = rewriter.Visit(tree.GetRoot());
    string newCode = newRoot.ToFullString();
    File.WriteAllText(filePath, newCode);
    Console.WriteLine("File reformatted.");
  }
}

class ClassReorderRewriter : CSharpSyntaxRewriter {

  public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) {
    var constantFields = new List<MemberInfo>();
    var staticFields = new List<MemberInfo>();
    var fields = new List<MemberInfo>();
    var constructors = new List<MemberInfo>();
    var properties = new List<MemberInfo>();
    var eventsDelegates = new List<MemberInfo>();
    var lifeCycleMethods = new List<MemberInfo>();
    var publicMethods = new List<MemberInfo>();
    var privateMethods = new List<MemberInfo>();
    var nestedTypes = new List<MemberInfo>();

    int currentGroupOrder = 0;
    int lastHeaderGroupOrder = -1;

    foreach (var member in node.Members) {
      var acc = GetAccessibility(member.Modifiers);
      int accessOrder = GetAccessOrder(acc);
      string name = GetName(member);

      // Detectar si el miembro tiene un atributo [Header]
      string headerValue = GetHeaderAttribute(member);
      bool hasHeader = !string.IsNullOrEmpty(headerValue);

      // Si encontramos un nuevo [Header], incrementamos el grupo
      if (hasHeader) {
        currentGroupOrder++;
        lastHeaderGroupOrder = currentGroupOrder;
      }

      // Los miembros sin [Header] que vienen después de un grupo con [Header]
      // pertenecen a ese grupo hasta encontrar otro [Header] o cambiar de tipo
      int memberGroupOrder = (lastHeaderGroupOrder >= 0) ? lastHeaderGroupOrder : currentGroupOrder;

      switch (member) {
        case FieldDeclarationSyntax fd:
          if (fd.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            constantFields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          else if (fd.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
            staticFields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          else
            fields.Add(new MemberInfo(fd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          break;
        case PropertyDeclarationSyntax p:
          properties.Add(new MemberInfo(p, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          break;
        case ConstructorDeclarationSyntax c:
          constructors.Add(new MemberInfo(c, accessOrder, name, false, "", 0));
          lastHeaderGroupOrder = -1; // Reset header group después de constructores
          break;
        case MethodDeclarationSyntax m:
          var methodName = m.Identifier.Text;
          if (new[] { "Awake", "OnEnable", "OnDisable", "OnDestroy" }.Contains(methodName))
            lifeCycleMethods.Add(new MemberInfo(m, accessOrder, methodName, false, "", 0));
          else {
            if (acc == Accessibility.Public || acc == Accessibility.Internal)
              publicMethods.Add(new MemberInfo(m, accessOrder, methodName, false, "", 0));
            else
              privateMethods.Add(new MemberInfo(m, accessOrder, methodName, false, "", 0));
          }
          lastHeaderGroupOrder = -1; // Reset header group después de métodos
          break;
        case EventDeclarationSyntax e:
          eventsDelegates.Add(new MemberInfo(e, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          break;
        case DelegateDeclarationSyntax d:
          eventsDelegates.Add(new MemberInfo(d, accessOrder, name, false, "", 0));
          lastHeaderGroupOrder = -1;
          break;
        case EventFieldDeclarationSyntax efd:
          eventsDelegates.Add(new MemberInfo(efd, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          break;
        case ClassDeclarationSyntax cd:
        case StructDeclarationSyntax sd:
        case InterfaceDeclarationSyntax id:
        case EnumDeclarationSyntax ed:
          nestedTypes.Add(new MemberInfo(member, accessOrder, name, false, "", 0));
          lastHeaderGroupOrder = -1;
          break;
        default:
          fields.Add(new MemberInfo(member, accessOrder, name, hasHeader, headerValue, memberGroupOrder));
          break;
      }
    }

    // Ordenar cada lista: AccessOrder -> GroupOrder -> los que tienen [Header] primero -> Name
    constantFields = SortMembers(constantFields);
    staticFields = SortMembers(staticFields);
    fields = SortMembers(fields);
    constructors = constructors.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
    properties = SortMembers(properties);
    eventsDelegates = SortMembers(eventsDelegates);
    lifeCycleMethods = lifeCycleMethods.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
    publicMethods = publicMethods.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
    privateMethods = privateMethods.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();
    nestedTypes = nestedTypes.OrderBy(x => x.AccessOrder).ThenBy(x => x.Name).ToList();

    // Combine
    var newMembers = new SyntaxList<MemberDeclarationSyntax>();
    foreach (var mi in constantFields.Concat(staticFields).Concat(fields).Concat(constructors).Concat(properties).Concat(eventsDelegates).Concat(lifeCycleMethods).Concat(publicMethods).Concat(privateMethods).Concat(nestedTypes)) {
      newMembers = newMembers.Add(mi.Node);
    }

    return node.WithMembers(newMembers);
  }

  private Accessibility GetAccessibility(SyntaxTokenList modifiers) {
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

  private int GetAccessOrder(Accessibility acc) {
    switch (acc) {
      case Accessibility.Public: return 0;
      case Accessibility.Internal: return 1;
      case Accessibility.Protected: return 2;
      case Accessibility.Private: return 3;
      case Accessibility.ProtectedAndInternal: return 1;
      default: return 3;
    }
  }

  // Extrae el valor del atributo [Header("...")]
  private string GetHeaderAttribute(MemberDeclarationSyntax member) {
    var attributeLists = member.AttributeLists;
    foreach (var attrList in attributeLists) {
      foreach (var attr in attrList.Attributes) {
        var attrName = attr.Name.ToString();
        if (attrName == "Header" || attrName == "HeaderAttribute") {
          if (attr.ArgumentList?.Arguments.Count > 0) {
            var firstArg = attr.ArgumentList.Arguments[0];
            var expression = firstArg.Expression;
            if (expression is LiteralExpressionSyntax literal) {
              return literal.Token.ValueText;
            }
          }
        }
      }
    }
    return "";
  }

  private string GetName(MemberDeclarationSyntax member) {
    switch (member) {
      case FieldDeclarationSyntax fd:
        return fd.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "";
      case PropertyDeclarationSyntax p:
        return p.Identifier.Text;
      case ConstructorDeclarationSyntax c:
        return c.Identifier.Text;
      case MethodDeclarationSyntax m:
        return m.Identifier.Text;
      case EventDeclarationSyntax e:
        return e.Identifier.Text;
      case DelegateDeclarationSyntax d:
        return d.Identifier.Text;
      case EventFieldDeclarationSyntax efd:
        return efd.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "";
      case ClassDeclarationSyntax cd:
        return cd.Identifier.Text;
      case StructDeclarationSyntax sd:
        return sd.Identifier.Text;
      case InterfaceDeclarationSyntax id:
        return id.Identifier.Text;
      case EnumDeclarationSyntax ed:
        return ed.Identifier.Text;
      default:
        return "";
    }
  }

  // Función de ordenamiento especial que mantiene grupos con [Header]
  private List<MemberInfo> SortMembers(List<MemberInfo> members) {
    return members
      .OrderBy(x => x.AccessOrder)           // 1. Por nivel de acceso
      .ThenBy(x => x.GroupOrder)              // 2. Por grupo de [Header]
      .ThenBy(x => x.HasHeader ? 0 : 1)       // 3. Los que tienen [Header] van primero en su grupo
      .ThenBy(x => x.Name)                    // 4. Alfabéticamente por nombre
      .ToList();
  }
  private class MemberInfo {
    public MemberDeclarationSyntax Node { get; set; }
    public int AccessOrder { get; set; }
    public string Name { get; set; }
    public bool HasHeader { get; set; }  // Indica si este miembro tiene [Header]
    public string HeaderValue { get; set; }  // Valor del [Header] si lo tiene
    public int GroupOrder { get; set; }  // Orden del grupo de header

    public MemberInfo(MemberDeclarationSyntax node, int accessOrder, string name, bool hasHeader, string headerValue, int groupOrder) {
      Node = node;
      AccessOrder = accessOrder;
      Name = name;
      HasHeader = hasHeader;
      HeaderValue = headerValue;
      GroupOrder = groupOrder;
    }
  }
}
