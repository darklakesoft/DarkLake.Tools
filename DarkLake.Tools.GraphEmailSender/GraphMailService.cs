 using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.IO;
using System.IO.Pipes;
using System.Net.Mail;

namespace DarkLake.Tools.GraphEmailSender
{
    public class GraphMailService
    {
        string tenantId = string.Empty;
        string clientId = string.Empty;
        string clientSecret = string.Empty;

        List<FileAttachment> files = new List<FileAttachment>();

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

            if (files != null && files.Count > 0) 
            {
                message.Attachments = new List<Microsoft.Graph.Models.Attachment>();   
            
                foreach (var file in files)
                {
                    message.Attachments.Add(file);
                }
            
            }



            Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody body = new()
            {
                Message = message,
                SaveToSentItems = true,  
                

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

        public void AddAttachment(string filePath)
        {
            if (files == null)
                files = new List<FileAttachment>();

            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                files.Add(new FileAttachment()
                {
                    Name = Path.GetFileName(filePath),
                    ContentType = "application/octet-stream",
                    ContentBytes = ReadFully(fileStream)

                });
            }
        }
     

        private byte[] ReadFully(Stream input)
        {
            using (var memoryStream = new MemoryStream())
            {
                input.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}