# Noggin Mail Forwarder
An SMTP Mail Server that can be configured to forward emails to another domain's address.

## Status
This server is in the development stages and does not work yet.

## Exposed Ports

The Docker container exposes the following ports for communication:

- Port 25: SMTP (Simple Mail Transfer Protocol)
- Port 587: Submission (SMTP submission)
- Port 465: SMTPS (SMTP over TLS/SSL)

## Setting the email rules
The server loads rules from docker environment variables when booting. Here is an example environment section:
```
environment:
    MailForwarder__Rules__0__alias: "*.awesome@alias-domain.co" 
    MailForwarder__Rules__0__address: "us@my-domain.co" 
    MailForwarder__Rules__1__alias: "*.stupid@alias-domain.co" 
    MailForwarder__Rules__1__address: "them@my-domain.co"
```
The numbers need to be sequential starting from zero and match for the two properties of each rule.

## How it works

This server relies on the following projects:

* [SmtpServer library](https://github.com/cosullivan/SmtpServer) by [Cain O'Sullivan](https://cainosullivan.com/).
* [DnsClient](https://github.com/MichaCo/DnsClient.NET)
* [MailKit](https://github.com/jstedfast/MailKit)

And here are some useful reference pages that I read:
* [SmtpServer issue on how to forward email](https://github.com/cosullivan/SmtpServer/issues/193)
* [Diving deep into emails: SMTP, envelopes, and headers](https://medium.com/@fabianterh/diving-deep-into-emails-smtp-envelopes-and-headers-a2367d1ad92)

## Testing with Telnet
If you're running the NogginForward server locally you can test with telnet.
```
telnet localhost 25
> 220 localhost v9.0.3.0 ESMTP ready

ehlo sender.co
> 250-localhost Hello sender.co, haven't we met before?

mail from:<test@sending-domain.co>
> 250 Ok

rcpt to:<someone.awesome@alias-domain.co>
> 250 Ok

data
> 354 end with <CRLF>.<CRLF>

Subject: Testing NogginForward Server

I do hope this works.
.

```
RCPT TO:<sender1@docker.localhost>
