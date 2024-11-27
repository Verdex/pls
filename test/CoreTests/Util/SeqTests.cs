using Pls.Core.Util;

namespace Pls.Test.Core.Util;

[TestFixture]
public class SeqTests {

    private abstract record Tree : ISeqable<Tree> {
        private Tree() { }

        public record Leaf(byte Value) : Tree;
        public record Node(Tree Right, Tree Left) : Tree;

        public Tree Self() => this;

        public IEnumerable<Tree> Next() 
            => this switch {
                Leaf => [],
                Node n => [n.Left, n.Right],
                _ => throw new NotImplementedException($"Unknown {nameof(Tree)} case {this.GetType()}"),
            };
            
    }

    [Test]
    public void ShouldTraverseSeqable() {
        var tree = new Tree.Node(new Tree.Node(new Tree.Leaf(0), new Tree.Leaf(1)), new Tree.Leaf(2));

        var output = tree.ToSeq().ToList();

        Assert.That(output, Is.EqualTo(new List<Tree> { tree
                                                      , new Tree.Node(new Tree.Leaf(0), new Tree.Leaf(1))
                                                      , new Tree.Leaf(0)
                                                      , new Tree.Leaf(1)
                                                      , new Tree.Leaf(2)
                                                      }));
    }

}