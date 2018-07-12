﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Microsoft.Identity.Client;
using Microsoft.Graph;

namespace send_actionable_message
{
    class Program
    {
        static PublicClientApplication authClient = null;
        static string[] scopes =
        {
            "User.Read", // Scope needed to read /Me from Graph (to get email address)
            "Mail.Send"  // Scope needed to send mail as the user
        };

        static void Main(string[] args)
        {
            SendMessage(args).Wait();
            Console.WriteLine("Hit any key to exit...");
            Console.ReadKey();
        }

        static async Task SendMessage(string[] args)
        {
            // Setup MSAL client
            authClient = new PublicClientApplication(ConfigurationManager.AppSettings.Get("applicationId"));

            try
            {
                // Get the access token
                var result = await authClient.AcquireTokenAsync(scopes);

                // Initialize Graph client with delegate auth provider
                // that just returns the token we already retrieved
                GraphServiceClient graphClient = new GraphServiceClient(
                    new DelegateAuthenticationProvider(
                        (requestMessage) =>
                        {
                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                            return Task.FromResult(0);
                        }));

                // Create a recipient for the authenticated user
                Microsoft.Graph.User me = await graphClient.Me.Request().GetAsync();
                Recipient toRecip = new Recipient()
                {
                    EmailAddress = new EmailAddress() { Address = me.Mail }
                };

                // Create the message
                Message actionableMessage = new Message()
                {
                    Subject = "Actionable message sent from code",
                    ToRecipients = new List<Recipient>() { toRecip },
                    Body = new ItemBody()
                    {
                        ContentType = BodyType.Html,
                        Content = LoadActionableMessageBody()
                    },
                    Attachments = new MessageAttachmentsCollectionPage()
                };

                // Create an attachment for the activity image
                FileAttachment actionImage = new FileAttachment()
                {
                    ODataType = "#microsoft.graph.fileAttachment",
                    Name = "activity_image", // IMPORTANT: Name must match ContentId
                    IsInline = true,
                    ContentId = "activity_image",
                    ContentType = "image/jpg",
                    ContentBytes = System.IO.File.ReadAllBytes(@".\ActivityImage.jpg")
                };

                actionableMessage.Attachments.Add(actionImage);

                // Send the message
                await graphClient.Me.SendMail(actionableMessage, true).Request().PostAsync();

                Output.WriteLine(Output.Success, "Message sent");
            }
            catch (MsalException ex)
            {
                Output.WriteLine(Output.Error, "An exception occurred while acquiring an access token.");
                Output.WriteLine(Output.Error, "  Code: {0}; Message: {1}", ex.ErrorCode, ex.Message);
            }
            catch (Microsoft.Graph.ServiceException graphEx)
            {
                Output.WriteLine(Output.Error, "An exception occurred while making a Graph request.");
                Output.WriteLine(Output.Error, "  Code: {0}; Message: {1}", graphEx.Error.Code, graphEx.Message);
            }
        }

        static string LoadActionableMessageBody()
        {
            // Load the card JSON
            string cardJson = System.IO.File.ReadAllText(@".\Card.json");

            // Insert the JSON into the HTML
            return string.Format(System.IO.File.ReadAllText(@".\MessageBody.html"), cardJson);
        }
    }
}
