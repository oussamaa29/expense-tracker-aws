namespace UpdateExpenseStatus
{
    /// <summary>
    /// Règles métier du cycle de vie d'une note de frais. Aucune dépendance à AWS :
    /// la classe se teste seule (voir tests/ExpenseTracker.Tests).
    /// </summary>
    public static class ExpenseStateMachine
    {
        private static readonly Dictionary<(string Status, string Action), string> Transitions = new()
        {
            { ("Draft",       "submit"),   "Submitted"   },
            { ("Submitted",   "approve"),  "Approved"    },
            { ("Submitted",   "reject"),   "Rejected"    },
            { ("Rejected",    "resubmit"), "Resubmitted" },
            { ("Resubmitted", "approve"),  "Approved"    },
            { ("Resubmitted", "reject"),   "Rejected"    },
        };

        public static readonly IReadOnlySet<string> FinanceActions = new HashSet<string> { "approve", "reject" };
        public static readonly IReadOnlySet<string> OwnerActions = new HashSet<string> { "submit", "resubmit" };

        /// <summary>Vérifie l'action demandée selon le rôle. Renvoie null si elle est autorisée.</summary>
        public static (int StatusCode, string Message)? Validate(string action, bool isFinance, string? comment)
        {
            if (!FinanceActions.Contains(action) && !OwnerActions.Contains(action))
                return (400, $"Unknown action: '{action}'.");
            if (FinanceActions.Contains(action) && !isFinance)
                return (403, "Only finance managers can approve or reject expenses.");
            if (OwnerActions.Contains(action) && isFinance)
                return (403, "Finance managers cannot submit or resubmit expenses.");
            if (action == "reject" && string.IsNullOrWhiteSpace(comment))
                return (400, "A comment is required when rejecting an expense.");
            return null;
        }

        /// <summary>Calcule le statut suivant. Renvoie false si la transition est interdite.</summary>
        public static bool TryTransition(string currentStatus, string action, out string newStatus)
        {
            if (Transitions.TryGetValue((currentStatus, action), out var next))
            {
                newStatus = next;
                return true;
            }
            newStatus = currentStatus;
            return false;
        }
    }
}
