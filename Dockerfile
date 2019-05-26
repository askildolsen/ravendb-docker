FROM ravendb/ravendb:4.2-ubuntu-latest

RUN apt-get update \
    && apt-get install -y \
    && apt-get install --no-install-recommends unzip -y

ADD https://www.nuget.org/api/v2/package/morelinq/3.1.0 /morelinq.zip
RUN unzip -j /morelinq.zip lib/netstandard2.0/MoreLinq.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/NetTopologySuite.Core/1.15.2 /NetTopologySuite.Core.zip
RUN unzip -o -j /NetTopologySuite.Core.zip lib/netstandard2.0/NetTopologySuite.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/NetTopologySuite.CoordinateSystems/1.15.2 /NetTopologySuite.CoordinateSystems.zip
RUN unzip -o -j /NetTopologySuite.CoordinateSystems.zip lib/netstandard2.0/NetTopologySuite.CoordinateSystems.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/ProjNET4GeoAPI/1.4.1 /ProjNET4GeoAPI.zip
RUN unzip -j /ProjNET4GeoAPI.zip lib/netstandard2.0/ProjNET.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/GeoAPI.CoordinateSystems/1.7.5 /GeoAPI.CoordinateSystems.zip
RUN unzip -j /GeoAPI.CoordinateSystems.zip lib/netstandard2.0/GeoAPI.CoordinateSystems.dll -d /opt/RavenDB/Server

ADD https://www.nuget.org/api/v2/package/GeoAPI.Core/1.7.5 /GeoAPI.Core.zip
RUN unzip -o -j /GeoAPI.Core.zip lib/netstandard2.0/GeoAPI.dll -d /opt/RavenDB/Server
