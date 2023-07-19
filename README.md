# Noggin Forward (SMTP Email Forwarding Server)
An SMTP Mail Server that can be configured to forward emails to another domain's address. It's designed to be run in a docker container and can be configured using a docker compose file.

## Status
This is the first version and works for me so far. I'd love other people to use it, but I wouldn't use it for business critical email just yet.

## Installing with docker compose

Here is a sample docker compose file:
```
version: "3.9" 
name: "your-forward-server" 
 
services: 
 
  #### Noggin Forward SMTP Server #### 
  forward-server:
    container_name: noggin-forward-server 
    image: nogginbox/nogginforward:main
    ports:
      - "25:25"
      - "587:587"
      - "465:465"
    restart: on-failure
    environment:
        MailForwarder__Rules__0__alias: "*.awesome@alias-domain.co" 
        MailForwarder__Rules__0__address: "us@my-domain.co" 
        MailForwarder__Rules__1__alias: "*.stupid@alias-domain.co" 
        MailForwarder__Rules__1__address: "them@my-domain.co"
```

### Exposed Ports

The Docker container exposes the following ports for communication:

- Port 25: SMTP (Simple Mail Transfer Protocol)
- Port 587: Submission (SMTP submission)
- Port 465: SMTPS (SMTP over TLS/SSL)

### Setting the email rules
The server loads rules from docker environment variables when booting. Each rule has two values, alias and address.

* alias: The email alias to check for as the recipient of incoming email. This can be a plain email address, or can contain ``*`` as a wildcard.
* address: This is the address to forward the email to and must be a valid email address.
* 

Each rule must include a sequential number starting from zero, like so:
```
environment:
    MailForwarder__Rules__0__alias: "*.awesome@alias-domain.co" 
    MailForwarder__Rules__0__address: "us@my-domain.co" 
    MailForwarder__Rules__1__alias: "*.stupid@alias-domain.co" 
    MailForwarder__Rules__1__address: "them@my-domain.co"
```


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

rcpt to:<us@my-domain.co>
> 250 Ok

data
> 354 end with <CRLF>.<CRLF>

Subject: Test Email
Date: Tue, 18 Jul 2023 21:41:00
From: test@docker.localhost
To: someone.awesome@alias-domain.co
Message-Id: <56d4ae71-e38c-4530-81ea-60a87546e9e7@docker.local>

Test content as descibed in the RFC http://www.faqs.org/rfcs/rfc2822.html.
.

```
