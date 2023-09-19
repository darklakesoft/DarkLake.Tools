using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net.Mail;

namespace DarkLake.Tools.GraphEmailSender
{
    public class GraphMailService
    {
        string tenantId = string.Empty;
        string clientId = string.Empty;
        string clientSecret = string.Empty;

        public GraphMailService() 
        {
            throw new NotImplementedException();
        }

        public GraphMailService(string TenantId, string ClientId, string ClientSecret) 
        {
            tenantId = TenantId;
            clientId = ClientId;
            clientSecret = ClientSecret;    
        }

        public bool Send(string toAddress, string fromAddress, string subject, string cc, string content, bool spam = false)
        {
            

            ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
            GraphServiceClient graphClient = new(credential);

            Message message = new()
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = Microsoft.Graph.Models.BodyType.Html,
                    Content = content
                },

            };

            if (!String.IsNullOrEmpty(cc))
            {
                message.CcRecipients = new List<Recipient>();

                foreach (var addresss in cc.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.CcRecipients.Add(new Recipient()
                    {
                        EmailAddress = new EmailAddress() { Address = addresss },
                    });
                }
            }

            message.ToRecipients = new List<Recipient>();
            foreach (var addresss in toAddress.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                message.ToRecipients.Add(new Recipient()
                {
                    EmailAddress = new EmailAddress() { Address = addresss },
                });
            }

            Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody body = new()
            {
                Message = message,
                SaveToSentItems = true,  // or true, as you want


            };

            if (spam)
            {
                var task = Task.Run(() => graphClient.Users[fromAddress].Messages.PostAsync(message));

                task.Wait();
                var response = task.Status;
            }
            else
            {
                var task = Task.Run(() => graphClient.Users[fromAddress].SendMail.PostAsync(body));

                task.Wait();
                var response = task.Status;
            }

            return true;
        }
    }
}