using System.Collections.Immutable;

using Pls.Core.Matching;

namespace Pls.Test.Core.Matching;

[TestFixture]
public class PatternTests {

    private static List<List<(string Name, Tree Item)>> F(Tree d, Pattern<Type, Tree> p) => d.Find<Type, Tree>(p).Select(x => x.ToList()).ToList();
    private static void A(List<List<(string Name, Tree Item)>> output, List<List<(string Name, Tree Item)>> expected) {
        Assert.Multiple(() => {
            Assert.That(output.Count, Is.EqualTo(expected.Count));
            foreach( var (o, e, i) in output.Zip(expected).Select((x, index) => (x.Item1, x.Item2, index) )) {
                Assert.That(o.Count, Is.EqualTo(e.Count), $"Index {i}");
                foreach( var (olet, elet) in o.Zip(e) ) {
                    Assert.That(olet.Name, Is.EqualTo(elet.Name), $"Index {i}");
                    Assert.That(olet.Item, Is.EqualTo(elet.Item), $"Index {i}");
                }
            }
        });
    }
    private static void D(List<List<(string Name, Tree Item)>> input) {
        foreach( var i in input ) {
            foreach( var ilet in i ) {
                Console.Write(ilet);
            }
            Console.Write("\n");
        }
    }

    private static Pattern<Type, Tree> Wild() => new Pattern<Type, Tree>.Wild();
    private static Pattern<Type, Tree> Exact(Type t, ImmutableList<Pattern<Type, Tree>> contents) => new Pattern<Type, Tree>.Exact(t, contents);
    private static Pattern<Type, Tree> Contents(ImmutableList<Pattern<Type, Tree>> contents) => new Pattern<Type, Tree>.Contents(contents);
    private static Pattern<Type, Tree> Kind(Type t) => new Pattern<Type, Tree>.Kind(t);
    private static Pattern<Type, Tree> And(Pattern<Type, Tree> a, Pattern<Type, Tree> b) => new Pattern<Type, Tree>.And(a, b);
    private static Pattern<Type, Tree> Or(Pattern<Type, Tree> a, Pattern<Type, Tree> b) => new Pattern<Type, Tree>.Or(a, b);
    private static Pattern<Type, Tree> Capture(string s) => new Pattern<Type, Tree>.Capture(s);
    private static Pattern<Type, Tree> TemplateVar(string s) => new Pattern<Type, Tree>.TemplateVar(s);
    private static Pattern<Type, Tree> SubContentPath(ImmutableList<Pattern<Type, Tree>> contents) => new Pattern<Type, Tree>.SubContentPath(contents);
    private static Pattern<Type, Tree> PathNext() => new Pattern<Type, Tree>.PathNext();
    private static Pattern<Type, Tree> Path(ImmutableList<Pattern<Type, Tree>> contents) => new Pattern<Type, Tree>.Path(contents);
    private static Pattern<Type, Tree> Predicate(Func<Tree, bool> predicate) => new Pattern<Type, Tree>.Predicate(predicate);
    private static Pattern<Type, Tree> MatchWith(Func<IReadOnlyDictionary<string, Tree>, Pattern<Type, Tree>> func) => new Pattern<Type, Tree>.MatchWith(func);


    [Test]
    public void FindWithWild() { 
        var t = Leaf(7);
        var output = F(t, Wild());
        A(output, [[]]);
    }

    [Test]
    public void FindWithExactTemplateVar() {
        var t = Node(Leaf(0), Leaf(0));
        var output = F(t, Exact(typeof(Tree.Node), [Capture("a"), TemplateVar("a")]));
        A(output, [[("a", Leaf(0))]]);
    }

    [Test]
    public void FindWithContentsTemplateVar() {
        var t = Node(Leaf(0), Leaf(0));
        var output = F(t, Contents([Capture("a"), TemplateVar("a")]));
        A(output, [[("a", Leaf(0))]]);
    }

    [Test]
    public void FindWithKind() {
        var t = Node(Leaf(0), Leaf(0));
        var output = F(t, Kind(typeof(Tree.Node)));
        A(output, [[]]);
    }

    [Test]
    public void FindWithPredicate() { 
        var t = Leaf(77);
        var output = F(t, Predicate(x => x is Tree.Leaf(Value: 77)));
        A(output, [[]]);
    }

    [Test]
    public void FindWithAnd() {
        var t = Leaf(77);
        var output = F(t, And(Predicate(x => x is Tree.Leaf(Value: 77)), Capture("x")));
        A(output, [[("x", Leaf(77))]]);
    }

    [Test]
    public void FindWithOr() {
        var t = Leaf(15);
        var output = F(t, Or(Wild(), Wild()));
        A(output, [[], []]);
    }

    [Test]
    public void FindWithMatchWith() {
        var t = Leaf(77);
        var output = F(t, MatchWith( _ => Wild()));
        A(output, [[]]);
    }

    [Test]
    public void FindWithSubContentPath() {
        var t = L(Leaf(1), Leaf(2), Leaf(3), Leaf(4));
        var output = F(t, SubContentPath([Capture("a"), Capture("b")]));
        A(output, [ [("a", Leaf(1)), ("b", Leaf(2))]
                  , [("a", Leaf(2)), ("b", Leaf(3))]
                  , [("a", Leaf(3)), ("b", Leaf(4))]
                  ]);
    }

    [Test]
    public void FindWithPath() {
        var t = Node(Node(Leaf(1), Leaf(2)), Node(Leaf(3), Leaf(4)));
        var output = F(t, Path([ Contents([PathNext(), PathNext()])
                               , Contents([PathNext(), PathNext()])
                               , Capture("a")
                               ]));
        A(output, [ [("a", Leaf(1))]
                  , [("a", Leaf(2))] 
                  , [("a", Leaf(3))] 
                  , [("a", Leaf(4))] 
                  ]);
    }

    [Test]
    public void FailTemplateWithUnknownName() {
        var t = Leaf(1);
        var output = F(t, TemplateVar("x"));
        A(output, []);
    }

    [Test]
    public void FailTemplateWithNonMatchingValue() {
        var t = Node(Leaf(1), Leaf(2));
        var output = F(t, Contents([Capture("x"), TemplateVar("x")]));
        A(output, []);
    }

    [Test]
    public void FindCaptureWithFirstOr() {
        var t = Leaf(1);
        var output = F(t, Or(Capture("a"), Exact(typeof(int), [])));
        A(output, [[("a", Leaf(1))]]);
    }

    [Test]
    public void FindCaptureWithSecondOr() {
        var t = Leaf(1);
        var output = F(t, Or(Exact(typeof(int), []), Capture("a")));
        A(output, [[("a", Leaf(1))]]);
    }

    [Test]
    public void FindCaptureWithBothOr() {
        var t = Leaf(1);
        var output = F(t, Or(Capture("a"), Capture("a")));
        A(output, [[("a", Leaf(1))], [("a", Leaf(1))]]);
    }

    [Test]
    public void FailOr() {
        var t = Leaf(1);
        var output = F(t, Or(Kind(typeof(int)), Kind(typeof(int))));
        A(output, []);
    }

    [Test]
    public void FailAndLeft() {
        var t = Leaf(1);
        var output = F(t, And(Kind(typeof(int)), Wild()));
        A(output, []);
    }

    [Test]
    public void FailAndRight() {
        var t = Leaf(1);
        var output = F(t, And(Wild(), Kind(typeof(int))));
        A(output, []);
    }

    [Test]
    public void FailAndBoth() {
        var t = Leaf(1);
        var output = F(t, And(Kind(typeof(int)), Kind(typeof(int))));
        A(output, []);
    }

    [Test]
    public void FailContentsForLength() {
        var t = Node(Leaf(1), Leaf(1));
        var output = F(t, Contents([Wild()]));
        A(output, []);
    }

    [Test]
    public void FailExactForLength() {
        var t = Node(Leaf(1), Leaf(1));
        var output = F(t, Exact(typeof(Tree.Node), [Wild()]));
        A(output, []);
    }

    [Test]
    public void FailExactForType() {
        var t = Node(Leaf(1), Leaf(1));
        var output = F(t, Exact(typeof(int), [Wild(), Wild()]));
        A(output, []);
    }

    [Test]
    public void FailPredicate() {
        var t = Leaf(1);
        var output = F(t, Predicate(_ => false));
        A(output, []);
    }

    [Test]
    public void FailMatchWith() {
        var t = Leaf(1);
        var output = F(t, MatchWith(_ => Predicate(_ => false)));
        A(output, []);
    }

    [Test]
    public void FindSubContentPathInPath() {
        var t = L([Leaf(1), Leaf(1), Leaf(2), Leaf(2), Leaf(3), Leaf(3)]);
        var output = F(t, Path([SubContentPath([Capture("a"), PathNext()]), TemplateVar("a")]));
        A(output, [[("a", Leaf(1))], [("a", Leaf(3))], [("a", Leaf(2))]]);
    }

    [Test]
    public void FindPathInSubContentPath() {
        var t = L([Node(Leaf(1), Leaf(2)), Leaf(3), Node(Leaf(4), Leaf(5)), Node(Leaf(5), Leaf(6)), Node(Leaf(6), Leaf(7))]);
        var output = F(t, SubContentPath([ Path([Exact(typeof(Tree.Node), [Wild(), Capture("a")])])
                                         , Path([Exact(typeof(Tree.Node), [TemplateVar("a"), Capture("b")])])
                                         ]));
        A(output, [[("a", Leaf(5)), ("b", Leaf(6))], [("a", Leaf(6)), ("b", Leaf(7))]]);
    }

    [Test]
    public void FindSubContentPathWithThreeCaptures() {
        var t = L([Leaf(1), Leaf(2), Leaf(3), Leaf(4), Leaf(5)]);
        var output = F(t, SubContentPath([ Capture("a"), Capture("b"), Capture("c") ]));
        A(output, [ [ ("a", Leaf(1)), ("b", Leaf(2)), ("c", Leaf(3)) ]
                  , [ ("a", Leaf(2)), ("b", Leaf(3)), ("c", Leaf(4)) ]
                  , [ ("a", Leaf(3)), ("b", Leaf(4)), ("c", Leaf(5)) ]
                  ]);
    }

    [Test]
    public void UseCaptureFromExactPatternInPath() {
        var t = Node(Leaf(1), L(Leaf(1), Leaf(2), Leaf(3)));
        var output = F(t, Exact(typeof(Tree.Node), [Capture("a"), 
            Path([Exact(typeof(Tree.L), [TemplateVar("a"), PathNext(), PathNext()])
                 , Capture("b")
                 ])]));
        A(output, [ [("a", Leaf(1)), ("b", Leaf(2))]
                  , [("a", Leaf(1)), ("b", Leaf(3))] 
                  ]);
    }

    [Test]
    public void UsePathCapturesFromExactPatternInPath() { 
        var t = Node(Node(Node(Leaf(1), Leaf(4)), Node(Leaf(1), Leaf(5))), L(Leaf(1), Leaf(2), Leaf(3)));
        var output = F(t, Exact(typeof(Tree.Node), [ Path( [ Exact(typeof(Tree.Node), [PathNext(), PathNext()])
                                                           , Exact(typeof(Tree.Node), [Capture("a"), Capture("c")])
                                                           ]), 
            Path([Exact(typeof(Tree.L), [TemplateVar("a"), PathNext(), PathNext()])
                 , Capture("b")
                 ])]));
        A(output, [ [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(2))]
                  , [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(3))] 
                  , [("a", Leaf(1)), ("c", Leaf(5)), ("b", Leaf(2))] 
                  , [("a", Leaf(1)), ("c", Leaf(5)), ("b", Leaf(3))] 
                  ]);
    }

    [Test]
    public void UsePathCapturesFromExactPatternInPathWithFailingInitialCaptures() { 
        var t = Node(Node(Node(Leaf(1), Leaf(4)), Node(Leaf(2), Leaf(5))), L(Leaf(1), Leaf(2), Leaf(3)));
        var output = F(t, Exact(typeof(Tree.Node), [ Path( [ Exact(typeof(Tree.Node), [PathNext(), PathNext()])
                                                           , Exact(typeof(Tree.Node), [Capture("a"), Capture("c")])
                                                           ] ), 
            Path([Exact(typeof(Tree.L), [TemplateVar("a"), PathNext(), PathNext()])
                 , Capture("b")
                 ])]));
        A(output, [ [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(2))]
                  , [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(3))] 
                  ]);
    }

    [Test]
    public void UsePathCapturesFromExactPatternInPathWithFailingSecondaryCaptures() { 
        var t = Node(Node(Node(Leaf(1), Leaf(4)), Node(Leaf(1), Leaf(5))), L(Leaf(0), Leaf(2), Leaf(3)));
        var output = F(t, Exact(typeof(Tree.Node), [ Path( [ Exact(typeof(Tree.Node), [PathNext(), PathNext()])
                                                           , Exact(typeof(Tree.Node), [Capture("a"), Capture("c")])
                                                           ]), 
            Path([Exact(typeof(Tree.L), [TemplateVar("a"), PathNext(), PathNext()])
                 , Capture("b")
                 ])]));
        A(output, []); 
    }


    [Test]
    public void UseSubContentPathCapturesFromExactPatternInPath() { 
        var t = Node(L(Leaf(1), Leaf(4), Leaf(1), Leaf(5)), L(Leaf(1), Leaf(2), Leaf(3)));
        var output = F(t, Exact(typeof(Tree.Node), [ SubContentPath( [ Capture("a"), Capture("c") ] ),
            Path([Exact(typeof(Tree.L), [TemplateVar("a"), PathNext(), PathNext()])
                 , Capture("b")
                 ])]));
        A(output, [ [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(2))]
                  , [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(3))] 
                  , [("a", Leaf(1)), ("c", Leaf(5)), ("b", Leaf(2))] 
                  , [("a", Leaf(1)), ("c", Leaf(5)), ("b", Leaf(3))] 
                  ]);
    }

    [Test]
    public void UseSubContentPathCapturesFromExactPatternInSubContentPath() {
        var t = Node(L(Leaf(1), Leaf(4), Leaf(2), Leaf(5)), L(Leaf(1), Leaf(2), Leaf(3)));
        var output = F(t, Exact(typeof(Tree.Node), [ SubContentPath( [ Capture("a"), Capture("c") ] ),
            SubContentPath([TemplateVar("a"), Capture("b")])]));
        A(output, [ [("a", Leaf(1)), ("c", Leaf(4)), ("b", Leaf(2))]
                  , [("a", Leaf(2)), ("c", Leaf(5)), ("b", Leaf(3))] 
                  ]);
    }

    [Test]
    public void SubContentPathShouldFailForShortData() {
        var t = Node(Leaf(1), Leaf(2));
        var output = F(t, SubContentPath([Wild(), Wild(), Wild()]));
        A(output, []);
    }

    [Test]
    public void SubContentPathShouldFailForShortDataInAlternative() {
        var t = Node(Leaf(1), Leaf(2));
        var output = F(t, Or(SubContentPath([Wild(), Wild(), Wild()]), Capture("a")));
        A(output, [[("a", Node(Leaf(1), Leaf(2)))]]);
    }

    [Test]
    public void ContentShouldFailForUnequalDataInAlternative() {
        var t = Node(Leaf(1), Leaf(2));
        var output = F(t, Or(Contents([Capture("a")]), Contents([Capture("b"), Capture("c")])));
        A(output, [[("b", Leaf(1)), ("c", Leaf(2))]]);
    }

    [Test]
    public void PredicateShouldFailInAlternative() {
        var t = Node(Leaf(1), Leaf(2));
        var output = F(t, Or(Predicate(_ => false), Capture("a")));
        A(output, [[("a", Node(Leaf(1), Leaf(2)))]]);
    }

    [Test]
    public void FindZeroLengthPath() {
        var t = Leaf(1);
        var output = F(t, Path([]));
        A(output, [[]]);
    }

    [Test]
    public void FindMatchWithInSubContentPath() {
        static Pattern<Type, Tree> With(IReadOnlyDictionary<String, Tree> dict) {
            if (dict["a"] is Tree.Leaf(var x)) {
                return Predicate(v => {
                    if (v is Tree.Leaf(var y)) {
                        return y == x + 1;
                    }
                    return false;
                });
            }
            return Wild();
        }
        var t = L(Leaf(1), Leaf(2), Leaf(3), Leaf(4), Leaf(7));
        var output = F(t, SubContentPath([Capture("a"), MatchWith(With)]));
        A(output, [[("a", Leaf(1))], [("a", Leaf(2))], [("a", Leaf(3))]]);
    }

    private static Tree Leaf(byte input) => new Tree.Leaf(input);
    private static Tree Node(Tree left, Tree right) => new Tree.Node(left, right);
    private static Tree L(params Tree[] xs) => new Tree.L(xs.ToList());

    private abstract record Tree : IMatchable<Type, Tree> {
        private Tree() { }

        public record Leaf(byte Value) : Tree;
        public record Node(Tree Left, Tree Right) : Tree;
        public record L(List<Tree> Items) : Tree;

        public void Deconstruct(out Type id, out IEnumerable<Tree> contents) {
            switch (this) {
                case Leaf l:
                    id = typeof(Leaf);
                    contents = [];
                    break;
                case Node n:
                    id = typeof(Node);
                    contents = [n.Left, n.Right];
                    break;
                case L l:
                    id = typeof(L);
                    contents = new List<Tree>(l.Items);
                    break;
                default:
                    throw new NotImplementedException($"Unknown {nameof(Tree)} case {this.GetType()}");
            }
        }
    }
}
