FROM ravendb/ravendb-nightly:4.2-ubuntu-latest

RUN apt-get update \
    && apt-get install -y \
    && apt-get install --no-install-recommends unzip -y

ADD https://www.nuget.org/api/v2/package/morelinq/3.1.0 /morelinq.zip
RUN unzip -j /morelinq.zip lib/netstandard2.0/MoreLinq.dll -d /opt/RavenDB/Server
