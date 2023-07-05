using MailKit;
using MailKit.Net.Smtp;
using MimeKit;

namespace Nogginbox.MailForwarder.TestsOfIntegration
{
	public class SendEmailToDocker
	{
		[Fact]
		public async Task SendEmailTest()
		{
			var sendEmail = await SendTestEmailAsync();
			Assert.NotNull(sendEmail);
		}


		private static async Task<string> SendTestEmailAsync()
		{
			// Email content
			string subject = $"Test Email - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
			string body = $"This is a test email sent at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.";

			// SMTP server settings
			string smtpServer = "localhost";
			int smtpPort = 587;//25;
			//string username = "your-username";
			//string password = "your-password";

			// Create the email message
			MimeMessage message = new ()
			{
				Subject = subject,
				Body = new TextPart("plain")
				{
					Text = body
				}
			};
			message.From.Add(new MailboxAddress("Sender", "test-sender@nogginbox.co.uk"));
			message.To.Add(new MailboxAddress("Recipient", "test-alias@test.com"));

			// Configure the SMTP client
			var logger = new ProtocolLogger("smtp.log");
			using var client = new SmtpClient(logger);
			try
			{
				client.Connect(smtpServer, smtpPort, false);
				//client.Authenticate(username, password);
			}
			catch (Exception ex)
			{
				return ex.Message;
			}

			string response;
			try
			{
				// Send the email
				response = await client.SendAsync(message);
			}
			catch (Exception ex)
			{
				response = $"An error occurred while sending the email: {ex.Message}";
			}

			client.Disconnect(true);
			return response;
		}
	}
}