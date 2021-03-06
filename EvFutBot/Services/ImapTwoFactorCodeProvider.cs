﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AE.Net.Mail;
using UltimateTeam.Toolkit.Exceptions;
using UltimateTeam.Toolkit.Services;

// Turn ON IMAP on google settings
// https://www.google.com/settings/security/lesssecureapps

namespace EvFutBot.Services
{
    public class ImapTwoFactorCodeProvider : ITwoFactorCodeProvider
    {
        private const int Port = 993;
        private const bool UseSsl = true;
        private readonly string _email;
        private readonly string _hostName;
        private readonly string _password;
        private readonly string _username;

        public ImapTwoFactorCodeProvider(string username, string password, string email)
        {
            _username = username;
            _password = password;
            _email = email;
            // gmail or 00k.pl
            _hostName = email.IndexOf("@gmail.com", StringComparison.Ordinal) != -1 ? "imap.gmail.com" : "imap.iq.pl";
        }

        public Task<string> GetTwoFactorCodeAsync()
        {
            return Task.Run(async () =>
            {
                using (var client = new ImapClient(_hostName, _username, _password, AuthMethods.Login, Port,
                    UseSsl))
                {
                    var rand = new Random();
                    var randDelay = rand.Next(60, 90);
                    await Task.Delay(randDelay*1000); // wait 60-90s  

                    var count = client.GetMessageCount();
                    int offset;
                    switch (_hostName)
                    {
                        case "imap.iq.pl":
                            count = count - 1; // stupid iq pl
                            offset = count - (count > 10 ? 10 : count);
                            break;
                        default:
                            offset = count - (count > 50 ? 50 : count);
                            break;
                    }

                    var mm = client.GetMessages(offset, count);
                    var code = GetEaCode(mm);
                    if (code.Length == 0)
                    {
                        // we get bodyes too if not found in subject
                        mm = client.GetMessages(offset, count, false);
                        code = GetEaCode(mm, @">\d{6}<");
                    }

                    if (code.Length != 0) return code;
                }
                throw new FutException("Unable to find the two-factor authentication code.");
            });
        }

        private string GetEaCode(MailMessage[] mm, string regex = @"\d{6}")
        {
            var code = string.Empty;
            Array.Reverse(mm);
            foreach (var message in
                mm.Where(m => m.From.Address == "EA@e.ea.com" && m.To.First().Address == _email &&
                              !m.Flags.HasFlag(Flags.Seen)))
            {
                var searchIn = message.Body ?? message.Subject;
                code = Regex.Match(searchIn, regex).Value; // we base our regex on a six digits code
                if (message.Body != null) code = Regex.Match(code, regex).Value; // we clear the > <

                if (code.Length != 0) return code;
            }
            return code;
        }
    }
}