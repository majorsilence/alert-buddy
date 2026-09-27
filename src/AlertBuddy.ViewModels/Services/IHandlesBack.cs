namespace AlertBuddy.ViewModels.Services
{
    /// <summary>
    /// A screen that answers the back button itself. Most screens do not: back steps to the previous screen. The alarm takeover does, by
    /// swallowing it, because back must never dismiss an alarm nobody has responded to (PLAN.md section 4.1: nothing exits by accident).
    /// </summary>
    public interface IHandlesBack
    {
        /// <summary>Handles the back button. Returns true if it did, so the navigator does nothing more.</summary>
        bool HandleBack ();
    }
}
