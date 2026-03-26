using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Cedar.Schema;
using Cedar.Types;
using Xunit;

namespace Cedar.Schema.Tests;

internal static class SchemaTestData
{
    public static string LoadFixture(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
    }

    public static string SampleCedar =>
        """
        @doc("Address information")
        @personal_information
        type Address = {
        	@also("town")
        	city: String,
        	country: Country,
        	street: String,
        	zipcode?: String
        };

        entity Country;

        entity User {
        	active: Bool,
        	address: Address,
        	email: String
        };

        action view appliesTo {
        	principal: User,
        	resource: User,
        	context: {
        		ip: ipaddr
        	}
        };

        namespace MyApp {
        	type Metadata = {
        		tags: Set<String>
        	};

        	entity Document {
        		title: String
        	};

        	action manage appliesTo {
        		principal: User,
        		resource: [Document, User]
        	};
        }
        """;

    public static string SampleJson =>
        """
        {
          "": {
            "entityTypes": {
              "Country": {},
              "User": {
                "shape": {
                  "type": "Record",
                  "attributes": {
                    "active": {
                      "type": "EntityOrCommon",
                      "name": "Bool"
                    },
                    "address": {
                      "type": "EntityOrCommon",
                      "name": "Address"
                    },
                    "email": {
                      "type": "EntityOrCommon",
                      "name": "String"
                    }
                  }
                }
              }
            },
            "actions": {
              "view": {
                "appliesTo": {
                  "principalTypes": [
                    "User"
                  ],
                  "resourceTypes": [
                    "User"
                  ],
                  "context": {
                    "type": "Record",
                    "attributes": {
                      "ip": {
                        "type": "EntityOrCommon",
                        "name": "ipaddr"
                      }
                    }
                  }
                }
              }
            },
            "commonTypes": {
              "Address": {
                "type": "Record",
                "attributes": {
                  "city": {
                    "type": "EntityOrCommon",
                    "name": "String",
                    "annotations": {
                      "also": "town"
                    }
                  },
                  "country": {
                    "type": "EntityOrCommon",
                    "name": "Country"
                  },
                  "street": {
                    "type": "EntityOrCommon",
                    "name": "String"
                  },
                  "zipcode": {
                    "type": "EntityOrCommon",
                    "name": "String",
                    "required": false
                  }
                },
                "annotations": {
                  "doc": "Address information",
                  "personal_information": ""
                }
              }
            }
          },
          "MyApp": {
            "entityTypes": {
              "Document": {
                "shape": {
                  "type": "Record",
                  "attributes": {
                    "title": {
                      "type": "EntityOrCommon",
                      "name": "String"
                    }
                  }
                }
              }
            },
            "actions": {
              "manage": {
                "appliesTo": {
                  "principalTypes": [
                    "User"
                  ],
                  "resourceTypes": [
                    "Document",
                    "User"
                  ]
                }
              }
            },
            "commonTypes": {
              "Metadata": {
                "type": "Record",
                "attributes": {
                  "tags": {
                    "type": "Set",
                    "element": {
                      "type": "EntityOrCommon",
                      "name": "String"
                    }
                  }
                }
              }
            }
          }
        }
        """;
}

internal static class SchemaAssert
{
    public static void JsonEqual(string expected, string actual)
    {
        JsonNode? expectedNode = JsonNode.Parse(expected);
        JsonNode? actualNode = JsonNode.Parse(actual);

        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode), $"Expected JSON:\n{expectedNode}\nActual JSON:\n{actualNode}");
    }

    public static void EquivalentByCedar(string expected, string actual)
    {
        Assert.Equal(SchemaDocument.UnmarshalCedar(expected).MarshalCedar(), SchemaDocument.UnmarshalCedar(actual).MarshalCedar());
    }

    public static void EquivalentByJson(string expected, string actual)
    {
        Assert.Equal(SchemaDocument.UnmarshalJson(expected).MarshalJson(), SchemaDocument.UnmarshalJson(actual).MarshalJson());
    }

    public static NamespaceDecl SingleNamespace(SchemaDocument document, string name)
    {
        Assert.True(document.Namespaces.TryGetValue(name, out NamespaceDecl? declaration));
        return declaration!;
    }

    public static RecordType RequireRecordType(SchemaType type)
    {
        return Assert.IsType<RecordType>(type);
    }

    public static TypeRef RequireTypeRef(SchemaType type)
    {
        return Assert.IsType<TypeRef>(type);
    }

    public static EntityType RequireEntityType(IReadOnlyList<EntityType> types, int index = 0)
    {
        return types[index];
    }
}
