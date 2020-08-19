FROM ravendb/ravendb:5.0.2-ubuntu.18.04-x64

RUN apt-get update \
    && apt-get install -y \
    && apt-get install --no-install-recommends unzip -y

ADD https://www.nuget.org/api/v2/package/morelinq/3.3.2 /morelinq.zip
RUN unzip -j /morelinq.zip lib/netstandard2.0/MoreLinq.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/NetTopologySuite/2.1.0 /NetTopologySuite.zip
RUN unzip -o -j /NetTopologySuite.zip lib/netstandard2.0/NetTopologySuite.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/ProjNET/2.0.0 /ProjNET.zip
RUN unzip -j /ProjNET.zip lib/netstandard2.0/ProjNET.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/geohash-dotnet/1.0.4 /geohash-dotnet.zip
RUN unzip -j /geohash-dotnet.zip lib/netstandard2.0/Geohash.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/SmartFormat.NET/2.5.2 /smartformat.net.zip
RUN unzip -j /smartformat.net.zip lib/netstandard2.0/SmartFormat.dll -d /opt/RavenDB/Server

COPY src/bin.tmp/netstandard2.0/ravendb-docker.dll /opt/RavenDB/Server/ravendb-docker.dll
