# Noggin Mail Forwarder
An SMTP Mail Server that can be configured to forward emails to another domain's address.

## Status
This server is in the development stages and does not work yet.

## Exposed Ports

The Docker container exposes the following ports for communication:

- Port 25: SMTP (Simple Mail Transfer Protocol)
- Port 587: Submission (SMTP submission)
- Port 465: SMTPS (SMTP over TLS/SSL)

## How it works

This server relies on the following projects:

* [SmtpServer library](https://github.com/cosullivan/SmtpServer) by [Cain O'Sullivan](https://cainosullivan.com/).
* [DnsClient](https://github.com/MichaCo/DnsClient.NET)
* [MailKit](https://github.com/jstedfast/MailKit)

And here are some useful reference pages that I read:
* [SmtpServer issue on how to forward email](https://github.com/cosullivan/SmtpServer/issues/193)
* [Diving deep into emails: SMTP, envelopes, and headers](https://medium.com/@fabianterh/diving-deep-into-emails-smtp-envelopes-and-headers-a2367d1ad92)