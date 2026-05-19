using ExpenseTracker.Views;

namespace ExpenseTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("EmployeePage",      typeof(EmployeePage));
        Routing.RegisterRoute("FinancePage",       typeof(FinancePage));
        Routing.RegisterRoute("ExpenseDetailPage", typeof(ExpenseDetailPage));
    }
}
