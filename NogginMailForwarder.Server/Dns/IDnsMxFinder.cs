﻿namespace NogginMailForwarder.Server.Dns;

public interface IDnsMxFinder
{
    Task<IList<string>> LookupMxServers(string hostAddress);
}
