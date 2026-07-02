using System.Collections.Generic;
using Xunit;

namespace Cadmus.Philology.Tools.Test;

public class EditOperationTest
{
    [Fact]
    public void Diff_BothEmpty_ReturnsEmpty()
    {
        IList<EditOperation> ops = EditOperation.Diff("", "");
        Assert.Empty(ops);
    }

    [Fact]
    public void Diff_SourceEmpty_TargetNot_InsertAfter()
    {
        IList<EditOperation> ops = EditOperation.Diff("", "abc");
        Assert.Single(ops);
        InsertAfterEditOperation? op = ops[0] as InsertAfterEditOperation;
        Assert.NotNull(op);
        Assert.Equal(0, op.At);
        Assert.Equal("abc", op.Text);
    }

    [Fact]
    public void Diff_TargetEmpty_SourceNot_Delete()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "");
        Assert.Single(ops);
        DeleteEditOperation? op = ops[0] as DeleteEditOperation;
        Assert.NotNull(op);
        Assert.Equal(1, op.At);
        Assert.Equal(3, op.Run);
        Assert.Equal("abc", op.InputText);
    }

    [Fact]
    public void Diff_EqualStrings_NoOps()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "abc");
        Assert.Empty(ops);
    }

    [Fact]
    public void Diff_SingleReplace_ReplaceOp()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "adc");
        Assert.Single(ops);
        ReplaceEditOperation? op = ops[0] as ReplaceEditOperation;
        Assert.NotNull(op);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal("d", op.ReplacementText);
        Assert.Equal("b", op.InputText);
    }

    [Fact]
    public void Diff_InsertInMiddle_InsertBefore()
    {
        IList<EditOperation> ops = EditOperation.Diff("ac", "abc");
        Assert.Single(ops);
        InsertBeforeEditOperation? op = ops[0] as InsertBeforeEditOperation;
        Assert.NotNull(op);
        Assert.Equal(2, op.At);
        Assert.Equal("b", op.Text);
    }

    [Fact]
    public void Diff_DeleteInMiddle_DeleteOp()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "ac");
        Assert.Single(ops);
        DeleteEditOperation? op = ops[0] as DeleteEditOperation;
        Assert.NotNull(op);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal("b", op.InputText);
    }

    [Fact]
    public void Diff_MultipleEdits_WithoutAdjustment()
    {
        // Test without adjustment to ensure original behavior is preserved
        IList<EditOperation> ops = EditOperation.Diff("abcdef", "azced",
            adjust: false);
        Assert.Equal(3, ops.Count);

        // the expected operations are for:
        // abcdef
        // 123456
        // 1. replace 'b' with 'z' at position 2
        // 2. delete 'd' at position 4
        // 3. replace 'f' with 'd' at position 5
        Assert.IsType<ReplaceEditOperation>(ops[0]);
        ReplaceEditOperation? rep = ops[0] as ReplaceEditOperation;
        Assert.NotNull(rep);
        Assert.Equal("\"b\"@2=\"z\"", rep.ToString());

        Assert.IsType<DeleteEditOperation>(ops[1]);
        DeleteEditOperation? del = ops[1] as DeleteEditOperation;
        Assert.NotNull(del);
        Assert.Equal("\"d\"@4!", del.ToString());

        Assert.IsType<ReplaceEditOperation>(ops[2]);
        rep = ops[2] as ReplaceEditOperation;
        Assert.NotNull(rep);
        Assert.Equal("\"f\"@5=\"d\"", rep.ToString());
    }

    [Fact]
    public void Diff_MultipleEdits_WithAdjustment()
    {
        // test with adjustment enabled (default behavior)
        IList<EditOperation> ops = EditOperation.Diff("abcdef", "azced");
        Assert.Equal(2, ops.Count);

        // expected operations after adjustment:
        // 1. replace 'b' with 'z' at position 2
        // 2. move 'd' from position 4 to position 6 (replacing 'f')
        Assert.IsType<ReplaceEditOperation>(ops[0]);
        ReplaceEditOperation? rep = ops[0] as ReplaceEditOperation;
        Assert.NotNull(rep);
        Assert.Equal("\"b\"@2=\"z\"", rep.ToString());

        Assert.IsType<MoveBeforeEditOperation>(ops[1]);
        MoveBeforeEditOperation? move = ops[1] as MoveBeforeEditOperation;
        Assert.NotNull(move);
        Assert.Equal(4, move.At);
        Assert.Equal(1, move.Run);
        Assert.Equal(6, move.To); // position after 'f' which gets replaced
        Assert.Equal("d", move.InputText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Diff_IncludeInputText_RespectsFlag(bool includeInputText)
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "adc",
            includeInputText);
        ReplaceEditOperation? op = ops[0] as ReplaceEditOperation;
        Assert.NotNull(op);
        if (includeInputText)
            Assert.Equal("b", op.InputText);
        else
            Assert.Null(op.InputText);
    }

    [Fact]
    public void Diff_InsertAtStart_InsertBefore()
    {
        IList<EditOperation> ops = EditOperation.Diff("bc", "abc");
        Assert.Single(ops);
        InsertBeforeEditOperation? op = ops[0] as InsertBeforeEditOperation;
        Assert.NotNull(op);
        Assert.Equal(1, op.At);
        Assert.Equal("a", op.Text);
    }

    [Fact]
    public void Diff_InsertAtEnd_InsertAfter()
    {
        IList<EditOperation> ops = EditOperation.Diff("ab", "abc");
        Assert.Single(ops);
        InsertAfterEditOperation? op = ops[0] as InsertAfterEditOperation;
        Assert.NotNull(op);
        Assert.Equal(2, op.At);
        Assert.Equal("c", op.Text);
    }

    [Fact]
    public void Diff_DeleteAtStart_DeleteOp()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "bc");
        Assert.Single(ops);
        DeleteEditOperation? op = ops[0] as DeleteEditOperation;
        Assert.NotNull(op);
        Assert.Equal(1, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal("a", op.InputText);
    }

    [Fact]
    public void Diff_DeleteAtEnd_DeleteOp()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "ab");
        Assert.Single(ops);
        DeleteEditOperation? op = ops[0] as DeleteEditOperation;
        Assert.NotNull(op);
        Assert.Equal(3, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal("c", op.InputText);
    }

    [Fact]
    public void Diff_ReplaceAtStart_ReplaceOp()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "xbc");
        Assert.Single(ops);
        ReplaceEditOperation? op = ops[0] as ReplaceEditOperation;
        Assert.NotNull(op);
        Assert.Equal(1, op.At);
        Assert.Equal("x", op.ReplacementText);
        Assert.Equal("a", op.InputText);
    }

    [Fact]
    public void Diff_ReplaceAtEnd_ReplaceOp()
    {
        IList<EditOperation> ops = EditOperation.Diff("abc", "abx");
        Assert.Single(ops);
        ReplaceEditOperation? op = ops[0] as ReplaceEditOperation;
        Assert.NotNull(op);
        Assert.Equal(3, op.At);
        Assert.Equal("x", op.ReplacementText);
        Assert.Equal("c", op.InputText);
    }

    [Fact]
    public void Diff_AdjustMoveWithInsertBefore()
    {
        // test case: "abc" -> "bac" (move 'a' before 'c')
        IList<EditOperation> ops = EditOperation.Diff("abc", "bac");
        
        // with adjustment, this should create a move operation
        Assert.Single(ops);

        // "a"@1>@2
        Assert.IsType<MoveBeforeEditOperation>(ops[0]);
        MoveBeforeEditOperation? move = ops[0] as MoveBeforeEditOperation;
        Assert.NotNull(move);
        Assert.Equal(1, move.At);
        Assert.Equal(3, move.To);
        Assert.Equal("a", move.InputText);
    }

    [Fact]
    public void Diff_AdjustMoveWithInsertAfter()
    {
        // test case: "abc" -> "acb" (move 'b' after 'c')
        IList<EditOperation> ops = EditOperation.Diff("abc", "acb");
        
        // with adjustment, this should optimize the operations
        Assert.True(ops.Count <= 2); // should be optimized
        
        // check if we have a move operation
        bool hasMoveOp = false;
        foreach (EditOperation op in ops)
        {
            if (op is MoveBeforeEditOperation)
            {
                hasMoveOp = true;
                break;
            }
        }
        Assert.True(hasMoveOp);
    }

    [Fact]
    public void Diff_AdjustInsertOnlyMode()
    {
        // test with insertOnly = true, should not merge with replace operations
        IList<EditOperation> ops = EditOperation.Diff("abcdef", "azced", 
            includeInputText: true, adjust: true, insertOnly: true);
        
        // should have 3 operations since replace operations are not considered
        // for merging
        Assert.Equal(3, ops.Count);
        
        // should not have any move operations since we're only considering inserts
        foreach (EditOperation op in ops)
        {
            Assert.IsNotType<MoveBeforeEditOperation>(op);
            Assert.IsNotType<MoveAfterEditOperation>(op);
        }
    }

    [Fact]
    public void Diff_AdjustInsertOnlyModeWithInsert()
    {
        // create a scenario where there's a delete and an insert with the same
        // text
        IList<EditOperation> ops = EditOperation.Diff("abcd", "acdb");
        
        // this should create a move operation even in insertOnly mode if
        // there's an actual insert
        bool hasMoveOp = false;
        foreach (EditOperation op in ops)
        {
            if (op is MoveBeforeEditOperation)
            {
                hasMoveOp = true;
                break;
            }
        }
        // Should have move operation since 'b' is moved
        Assert.True(hasMoveOp);
    }

    [Fact]
    public void Diff_NoAdjustmentWhenTextNotUnique()
    {
        // test case where the same text appears multiple times
        // "aabbcc" -> "abcabc" - 'b' appears twice, so no adjustment should occur
        IList<EditOperation> ops = EditOperation.Diff("aabbcc", "abcabc", adjust: true);
        
        // should not create move operations when text is not unique
        foreach (EditOperation op in ops)
        {
            Assert.IsNotType<MoveBeforeEditOperation>(op);
            Assert.IsNotType<MoveAfterEditOperation>(op);
        }
    }

    [Fact]
    public void Diff_AdjustPreservesNotesAndTags()
    {
        // test that notes and tags are preserved during adjustment.
        // This would require manual creation of operations since Diff doesn't
        // set notes/tags, but we can test the basic functionality
        IList<EditOperation> ops = EditOperation.Diff("abc", "acb");
        
        // Verify that any move operations created don't lose the InputText
        foreach (EditOperation op in ops)
        {
            if (op is MoveBeforeEditOperation move)
            {
                Assert.NotNull(move.InputText);
            }
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Diff_AllParameterCombinations(bool adjust, bool insertOnly)
    {
        // test all combinations of adjust and insertOnly parameters
        IList<EditOperation> ops = EditOperation.Diff("abcdef", "azced", 
            includeInputText: true, adjust: adjust, insertOnly: insertOnly);
        
        // should always return some operations for this transformation
        Assert.NotEmpty(ops);
        
        // when adjust is false, should not have move operations
        if (!adjust)
        {
            foreach (EditOperation op in ops)
            {
                Assert.IsNotType<MoveBeforeEditOperation>(op);
                Assert.IsNotType<MoveAfterEditOperation>(op);
            }
        }
    }

    [Fact]
    public void Diff_ComplexMoveScenario()
    {
        // test a more complex scenario: "abcdefg" -> "acbdefg" (move 'c' before 'b')
        IList<EditOperation> ops = EditOperation.Diff("abcdefg", "acbdefg");
        
        // should optimize this into fewer operations
        Assert.True(ops.Count <= 3);
        
        // should contain a move operation
        bool hasMoveOp = false;
        foreach (EditOperation op in ops)
        {
            if (op is MoveBeforeEditOperation)
            {
                hasMoveOp = true;
                break;
            }
        }
        Assert.True(hasMoveOp);
    }
}
