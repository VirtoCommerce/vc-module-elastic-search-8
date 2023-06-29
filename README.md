# Virto Commerce Elastic Search 8.x Module Preview version

## Overview
This is a preview version of VirtoCommerce Elastic Search 8.x module that uses the new .NET client for Elasticsearch. The module implements Elasticsearch CRUD operations defined in `ISeachProvider` interface but since it's a preview version some features might not work.

## Know Limitations & Issues
* Catalog object serialization via "Store serialized catalog objects in the index" platform settings is not implemented. Document field "__object" will not be indexed.
* Filtered aggregations are currently not implemented in ElasticSearch .NET client, will throw NotImplementedException. 
* BlueGreen indexation is not implemented.
* Partial indexation is not implemented.

## Configuration
The Elastic Search provider can be configured using the following keys:

* **Search.Provider**: Specifies the search provider name, which must be set to "ElasticSearch".
* **Search.Scope**: Specifies the common name (prefix) for all indexes. Each document type is stored in a separate index, and the full index name is scope-{documenttype}. This allows one search service to serve multiple indexes. (Optional: Default value is "default".)
* **Search.ElasticSearch.Server**: Specifies the network address and port of the Elasticsearch server.
* **Search.ElasticSearch.User**: Specifies the username for the Elasticsearch server.
* **Search.ElasticSearch.Key**: Specifies the password for the Elasticsearch server.
* **Search.ElasticSearch.CertificateFingerprint**: During development, you can provide the server certificate fingerprint. When present, it is used to validate the certificate sent by the server. The fingerprint is expected to be the hex string representing the SHA256 public key fingerprint. (Optional)


## Samples
Here are some sample configurations for different scenarios:

### Elasticsearch v8.x server
For local v8.x server, use the following configuration:

```json
"Search": {
    "Provider": "ElasticSearch8x",
    "Scope": "default",
    "ElasticSearch8x": {
        "Server": "https://localhost:9200",
        "User": "elastic",
        "Key": "{PASSWORD}"
    }
}
```

### Elastic Cloud v8.x
For Elastic Cloud v8.x, use the following configuration:

```json
"Search": {
    "Provider": "ElasticSearch",
    "Scope": "default",
    "ElasticSearch": {
        "Server": "https://4fe3ad462de203c52b358ff2cc6fe9cc.europe-west1.gcp.cloud.es.io:9243",
        "User": "elastic",
        "Key": "{SECRET_KEY}"
    }
}
```

## Documentation
* [Search Fundamentals](https://virtocommerce.com/docs/fundamentals/search/)
* [Elastic .NET Client](https://www.elastic.co/guide/en/elasticsearch/client/net-api/master/introduction.html)

## References
* Deployment: https://docs.virtocommerce.org/docs/latest/developer-guide/deploy-module-from-source-code/
* Installation: https://docs.virtocommerce.org/docs/latest/user-guide/modules/
* Home: https://virtocommerce.com
* Community: https://www.virtocommerce.org

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
