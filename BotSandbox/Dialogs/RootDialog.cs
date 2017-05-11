using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace BotSandbox.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            await context.PostAsync($"Hi! So we're going to help you open an account today!");
            context.Call(MakeOpenAccountDialog(), OpenAccountDialogComplete);
        }


        private IDialog<object> MakeOpenAccountDialog()
        {
            return Chain.From(

                // Prompt for Email
                () => new PromptDialog.PromptString("Enter your email:", "", 1)
                .While(
                    (email) => Chain.Return<bool>(!email.Contains("@")), //naive email check
                    (email) => new PromptDialog.PromptString($"'{email}' is not a valid email address. Enter it again:", "", 1)
                 )


                 // Validate email ownership
                 .ContinueWith<string, EmailAndCode>(async (context, result) =>
                 {
                     var email = await result;

                     // store the value in the bot state service
                     context.PrivateConversationData.SetValue("email", email);

                     GenerateConfirmationCodeAndSendEmail(email);

                     // wrap the state we send to the next While loop, because we need the email AND the code to check if the code is correct                     
                     //return new PromptDialog.PromptString($"Check your email and enter the confirmation code: ", "", 1);

                     return new PromptDialog.PromptString($"Check your email and enter the confirmation code: ", "", 1)
                            .ContinueWith(async (context1, result1) =>
                            {
                                var code = await result1;
                                var email1 = context1.PrivateConversationData.GetValueOrDefault<string>("email");
                                return Chain.Return(new EmailAndCode() { Email = email1, Code = code });
                            });
                         
                 })
                 .While<EmailAndCode>(
                    (emailAndCode) => Chain.Return(!CheckConfirmationCode(emailAndCode.Email, emailAndCode.Code)),

                    (emailAndCode) => // code is invalid
                    {
                        return Chain.From(
                            () => new PromptDialog.PromptChoice<string>(new string[] { "Enter Again", "Send me a new Code" }, "That's not the right code.", "Please select what you'd like to do.", 3))
                            .ContinueWith<string, string>(async (context, result) =>
                            {
                                var choice = await result;
                                if (choice == "Enter Again")
                                {
                                    return new PromptDialog.PromptString($"Please re-enter the confirmation code: ", "", 1);
                                }
                                else
                                {
                                    var email = context.PrivateConversationData.GetValueOrDefault<string>("email");
                                    GenerateConfirmationCodeAndSendEmail(email);
                                    return new PromptDialog.PromptString($"Check your email again, and enter the new confirmation code: ", "", 1);
                                }
                            })
                            .ContinueWith<string, EmailAndCode>(async (context1, result1) =>
                             {
                                 var code = await result1;
                                 var email1 = context1.PrivateConversationData.GetValueOrDefault<string>("email");
                                 return Chain.Return(new EmailAndCode() { Email = email1, Code = code });
                             });
                            }
                  )
                 );
        }

        private void GenerateConfirmationCodeAndSendEmail(string email)
        {
            // TODO generate code, save to database, send email
        }

        private bool CheckConfirmationCode(string email, string code)
        {
            // TODO check the database is the code is a match  
            return code == "ABC";
        }

        private async Task OpenAccountDialogComplete(IDialogContext context, IAwaitable<object> result)
        {
            var email = context.PrivateConversationData.GetValueOrDefault<string>("email");
            await context.PostAsync($"Thank you for opening a new account! We're going to use your email {email}.");
            context.Wait(MessageReceivedAsync);
        }
    }

    [Serializable]
    public class EmailAndCode
    {
        public string Email { get; set; }
        public string Code { get; set; }
    }

}