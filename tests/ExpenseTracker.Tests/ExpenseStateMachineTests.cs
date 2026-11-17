using UpdateExpenseStatus;

namespace ExpenseTracker.Tests;

public class ExpenseStateMachineTests
{
    [Theory]
    [InlineData("Draft", "submit", "Submitted")]
    [InlineData("Submitted", "approve", "Approved")]
    [InlineData("Submitted", "reject", "Rejected")]
    [InlineData("Rejected", "resubmit", "Resubmitted")]
    [InlineData("Resubmitted", "approve", "Approved")]
    [InlineData("Resubmitted", "reject", "Rejected")]
    public void Allowed_transitions_give_the_next_status(string current, string action, string expected)
    {
        Assert.True(ExpenseStateMachine.TryTransition(current, action, out var next));
        Assert.Equal(expected, next);
    }

    [Theory]
    [InlineData("Draft", "approve")]       // un brouillon n'a pas été soumis
    [InlineData("Draft", "resubmit")]
    [InlineData("Submitted", "submit")]    // déjà soumis
    [InlineData("Approved", "reject")]     // une dépense approuvée est définitive
    [InlineData("Approved", "resubmit")]
    [InlineData("Rejected", "approve")]    // il faut d'abord la resoumettre
    public void Forbidden_transitions_keep_the_current_status(string current, string action)
    {
        Assert.False(ExpenseStateMachine.TryTransition(current, action, out var next));
        Assert.Equal(current, next);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public void Only_finance_can_review(string action)
    {
        var error = ExpenseStateMachine.Validate(action, isFinance: false, comment: "motif");
        Assert.Equal(403, error?.StatusCode);
    }

    [Theory]
    [InlineData("submit")]
    [InlineData("resubmit")]
    public void Finance_cannot_submit_on_behalf_of_employees(string action)
    {
        var error = ExpenseStateMachine.Validate(action, isFinance: true, comment: null);
        Assert.Equal(403, error?.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejecting_requires_a_comment(string? comment)
    {
        var error = ExpenseStateMachine.Validate("reject", isFinance: true, comment);
        Assert.Equal(400, error?.StatusCode);
    }

    [Fact]
    public void Unknown_action_is_a_bad_request()
    {
        var error = ExpenseStateMachine.Validate("delete", isFinance: true, comment: null);
        Assert.Equal(400, error?.StatusCode);
    }

    [Theory]
    [InlineData("submit", false, null)]
    [InlineData("resubmit", false, null)]
    [InlineData("approve", true, null)]
    [InlineData("reject", true, "Justificatif illisible")]
    public void Valid_requests_pass(string action, bool isFinance, string? comment)
    {
        Assert.Null(ExpenseStateMachine.Validate(action, isFinance, comment));
    }
}
