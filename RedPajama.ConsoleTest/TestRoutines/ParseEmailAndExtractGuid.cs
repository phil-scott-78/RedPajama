using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseEmailAndExtractGuid : ITestRoutine
{
    public class UserRecord
    {
        public required string Username { get; init; }
        public required string Email { get; init; }
        public required Guid UserId { get; init; }
        public DateTime RegisteredDate { get; init; }
        public required string SubscriptionTier { get; init; }
    }
   
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);

        var prompt = """
                     Extract the user details from this email notification:

                     ```
                     From: system@ourplatform.com
                     To: support@ourplatform.com
                     Subject: New Premium Subscription

                     Hello Support Team,

                     This is an automated notification to inform you that user johndoe42 (john.doe@example.com) has upgraded to our Premium subscription tier on November 15th, 2023 at 2:30:22 PM

                     For reference, this user's unique identifier in our system is 8f1b6054-6c1a-4d85-b9a5-78e0b853d760.

                     Please ensure their account has been properly flagged for premium benefits and that all relevant features have been enabled. You can verify this by checking the user dashboard.

                     Regards,
                     Customer Management System
                     ```
                     """;
        
        UserRecord user;
        if (!model.IsThinkingModel())
        {
            user = await executor.InferAsync<UserRecord>(prompt, new TypeModelContext());
        }
        else
        {
            (user, _) = (await executor.InferWithThoughtsAsync<UserRecord>(prompt));
        }

        user.ShouldAllBe([
            u => u.Username.ShouldBe("johndoe42"),
            u => u.Email.ShouldBe("john.doe@example.com"),
            u => u.UserId.ShouldBe(new Guid("8f1b6054-6c1a-4d85-b9a5-78e0b853d760")),
            u => u.RegisteredDate.ShouldBe(new DateTime(2023, 11, 15, 14,30,22)),
            u => u.SubscriptionTier.ShouldBe("Premium")
        ]);
    }
}