# ObjectARX Dynamic-Block Evaluation Graph API (AcDbEval* classes)

Complete API surface for the six ObjectARX evaluation-graph classes, collected from the
**official Autodesk ObjectARX documentation** (help.autodesk.com, OARX 2025 ENU RefGuide).

## Provenance & verification

- Primary source: `https://help.autodesk.com/view/OARX/2025/ENU/?guid=...` pages. The site is a
  JavaScript app, but the underlying content is served as static HTML at
  `https://help.autodesk.com/cloudhelp/2025/ENU/OARX-RefGuide/files/<topic>.html` (discovered via the
  beehive REST endpoint `https://beehive.autodesk.com/community/service/rest/cloudhelp/resource/cloudhelpchannel/bookmark/?p=OARX&v=2025&l=ENU&guid=<guid>`).
- All content below was retrieved from those official pages (66 method/constructor/operator detail pages,
  6 class-overview pages, 6 methods-index pages, plus the AcDbEvalGraph NodeId enum and AcDbEvalVariant
  constructor/operator lists).
- **Version check**: the OARX **2024** pages for all six classes are byte-identical to the 2025 pages except
  version metadata, and the **OARXMAC 2024** (AutoCAD for Mac, .NET) RefGuide lists the exact same method
  sets for all six classes. The API surface below is therefore valid for 2024 and 2025, C++ and .NET.
- All descriptions are verbatim from the official docs (HTML tags stripped). Where the official docs
  themselves contain typos or placeholder text, that is preserved and flagged.
- All six classes live in the SDK header **`dbeval.h`** (per the official class-overview pages).

---

## AcDbEvalGraph

**File:** `dbeval.h`  
**C++:** `class AcDbEvalGraph : public AcDbObject;`  
**Class hierarchy:** `AcRxObject > AcGiDrawable > AcDbObject > AcDbEvalGraph`

**Official class description:**

> This class holds the network of interrelated elements that implement the behavior of dynamic blocks. Each individual element (or AcDbEvalExpr ) can depend on zero or more other AcDbEvalExpr objects. The role of AcDbEvalGraph is to invoke the AcDbEvalExpr::evaluate() method for each of the elements it contains at the proper time. Before an AcDbEvalExpr can be evaluated, evaluate() must be invoked for any dependent AcDbEvalExpr objects. AcDbEvalGraph uses a directed acyclic graph (DAG) to reprsent the dependencies between AcDbEvalExpr objects. If an AcDbEvalExpr E1 depends on (requires input from) an AcDbEvalExpr E2, an edge from E2 to E1 is represented in the graph. See the ObjectARX Developer's Guide for more information.
> Based on which AcDbEvalExpr objects are active during evaluation (in other words, which objects have been directly modified through means such as the Properties palette or grip editing), the subset of AcDbEvalExpr objects that can be reached from the activated set is determined. The resulting subgraph is expected to be a DAG, and is then topologically sorted to determine the order of node evaluation.
> The current internal use of this graph class is for dynamic blocks, in which a graph of the expressions is maintained on the block table record. This graph is queried at graph editing time and evaluated in order to trigger the networked nodes ( AcDbEvalExpr objects) that, through their evaluate() methods being invoked, implement the dynamic behavior.

**Related doc pages (official Links section):** AcDbEvalGraph Enumerations , AcDbEvalGraph Methods

**See Also:** AcDbEvalExpr

### Enumerations

One enumeration is documented for this class:

| Enumeration | Description (verbatim) |
|---|---|
| `NodeId` | This enum specifies special `AcDbEvalNodeId` values. |

C++ definition (verbatim from the official NodeId topic page):

```cpp
enum NodeId {
  kNullNodeId = 0
};
```

| Member | Description (verbatim) |
|---|---|
| `kNullNodeId` | Null node ID |

### Methods

Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):

| Method | Flags | Description (verbatim from index page) |
|---|---|---|
| `activate` | virtual | _(no description on index page)_ |
| `addEdge` | virtual | _(no description on index page)_ |
| `addGraph` | virtual | Adds the nodes from one graph to another graph by moving the nodes and edges from the source graph to this graph.All nodes and edges in pGraphToAdd are moved to the destination graph.Returns Acad::eOk if successful. |
| `addNode` | virtual | Adds a node to the graph and returns the AcDbEvalNodeId of the newly added node.If the graph is database resident, the caller must call close() on pNode when it is no longer needed.Returns Acad::eOk if successful. |
| `createGraph` | — | _(no description on index page)_ |
| `equals` | virtual | Determines whether two graphs are equal. Graph A is said to equal Graph B if A is a subgraph of B and B is a subgraph of A. See the isSubgraphOf() function for a definition of subgraphs.Returns true if this graph equals pOther . Otherwise, returns false. |
| `evaluate` | virtual | _(no description on index page)_ |
| `getAllNodes` | virtual | Returns an array of all node IDs contained in the graph.Returns Acad::eOk if successful. |
| `getEdgeInfo` | virtual | Returns information about an edge between two nodes in a graph.Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with either of the specified IDs does not exist in the graph. Returns Acad::eGraphEdgeNotFound if no edge exists between the nodes. |
| `getGraph` | — | _(no description on index page)_ |
| `getIncomingEdges` | virtual | Returns a list of incoming edges to a node in the graph.Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph. |
| `getIsActive` | virtual | Determines whether the specified node is activated in the graph.Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph. |
| `getNode` | virtual | Opens a node in the graph given its node ID.Callers must call close() on the returned node pointer when it is no longer needed.Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph. |
| `getOutgoingEdges` | virtual | Returns a list of outgoing edges from a node in the graph.Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph. |
| `hasGraph` | — | _(no description on index page)_ |
| `isSubgraphOf` | virtual | This function determines whether this graph is a subgraph of pOther . Before making this determination, the graph verifies that the node ID of each node in this graph matches that of a node in pOther . If any node ID in this graph is not found in pOther , this function returns false.If every node in this graph can be mapped to a node in pOther , the order and types of both the incoming and outgoing edges from each node in this graph must be found to be a subset of the incoming and outgoing edges for... more |
| `postInDatabase` | virtual | Adds this graph to the database, giving it a handle and an object ID. The new object ID is returned in the objId argument. All of this graph's nodes are added to the database and this graph is made their owner. This function does not establish ownership of this graph.Returns Acad::eOk if successful. |
| `removeEdge` | virtual | Removes an edge between two nodes in the graph.Returns Acad::eOk if successful. Returns Acad::eGraphEdgeNotFound if an edge between the nodes does not exist. |
| `removeGraph` | — | _(no description on index page)_ |
| `removeNode` | virtual | _(no description on index page)_ |
| `replaceGraph` | — | Replaces a graph on the object.Returns Acad::eOk if successful, Acad::eWrongObjectType if wrong type of object (i.e. not an AcDbEvalGraph ). |

Full detail for every method (verbatim C++ signature, description, parameters):

#### `AcDbEvalGraph::activate` (3 overloads)

##### Overload: `AcDbEvalGraph::activate (AcDbEvalNodeIdArray, AcDbEvalNodeIdArray, AcDbEvalNodeIdArray)`

```cpp
virtual Acad::ErrorStatus activate(
const AcDbEvalNodeIdArray& activatedNodes, 
AcDbEvalNodeIdArray* pActiveSubgraph, 
AcDbEvalNodeIdArray* pCycleNodes
) const;
```

**Description (verbatim):** Activates a collection of nodes in a graph. Applications must activate nodes in a graph before calling AcDbEvalGraph::evaluate() . Active nodes are used as the starting point for the directed traversal of the graph during graph evaluation. If activatedNodes is empty, all of the nodes in the graph are deactivated. Returns Acad::eOk if successful. Returns Acad::eGraphCyclesFound if the node activation resulted in a cyclic graph.

| Parameter | Description (verbatim) |
|---|---|
| `activatedNodes` | Input array of AcDbEvalNodeId objects of the nodes in the graph to activate |
| `pActiveSubgraph` | Array of nodes that would be visited given the activated nodes |
| `pCycleNodes` | Array of nodes that are cyclic given the activated nodes |

##### Overload: `AcDbEvalGraph::activate (AcDbEvalNodeIdArray, AcDbEvalNodeIdArray)`

```cpp
virtual Acad::ErrorStatus activate(
const AcDbEvalNodeIdArray& activatedNodes, 
AcDbEvalNodeIdArray* pActiveSubgraph
) const;
```

**Description (verbatim):** Activates a collection of nodes in a graph. Applications must activate nodes in a graph before calling AcDbEvalGraph::evaluate() . Active nodes are used as the starting point for the directed traversal of the graph during graph evaluation. If activatedNodes is empty, all of the nodes in the graph are deactivated. Returns Acad::eOk if successful. Returns Acad::eGraphCyclesFound if the node activation resulted in a cyclic graph.

| Parameter | Description (verbatim) |
|---|---|
| `activatedNodes` | Input array of AcDbEvalNodeId objects of the nodes in the graph to activate |
| `pActiveSubgraph` | Array of nodes that would be visited given the activated nodes |

##### Overload: `AcDbEvalGraph::activate (AcDbEvalNodeIdArray)`

```cpp
virtual Acad::ErrorStatus activate(
const AcDbEvalNodeIdArray& activatedNodes
) const;
```

**Description (verbatim):** Activates a collection of nodes in a graph. Applications must activate nodes in a graph before calling AcDbEvalGraph::evaluate() . Active nodes are used as the starting point for the directed traversal of the graph during graph evaluation. If activatedNodes is empty, all of the nodes in the graph are deactivated. Returns Acad::eOk if successful. Returns Acad::eGraphCyclesFound if the node activation resulted in a cyclic graph.

| Parameter | Description (verbatim) |
|---|---|
| `activatedNodes` | Input array of AcDbEvalNodeId objects of the nodes in the graph to activate |

#### `AcDbEvalGraph::addEdge` (2 overloads)

##### Overload: `AcDbEvalGraph::addEdge (AcDbEvalNodeId, AcDbEvalNodeId)`

```cpp
virtual Acad::ErrorStatus addEdge(
const AcDbEvalNodeId& idFrom, 
const AcDbEvalNodeId& idTo
);
```

**Description (verbatim):** Adds a non-invertible edge between two nodes in the graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if either of the nodes is not resident in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `idFrom` | Input AcDbEvalNodeId of the node from which the edge originates |
| `idTo` | Input AcDbEvalNodeId of the node at which the edge terminates |

##### Overload: `AcDbEvalGraph::addEdge (AcDbEvalNodeId, AcDbEvalNodeId, bool)`

```cpp
virtual Acad::ErrorStatus addEdge(
const AcDbEvalNodeId& idFrom, 
const AcDbEvalNodeId& idTo, 
bool bInvertible
);
```

**Description (verbatim):** Adds an edge between two nodes in the graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if either of the nodes is not resident in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `idFrom` | Input AcDbEvalNodeId of the node from which the edge originates |
| `idTo` | Input AcDbEvalNodeId of the node at which the edge terminates |
| `bInvertible` | Input Boolean indicating whether the edge can be inverted depending on which of the nodes has been activated |

#### `AcDbEvalGraph::addGraph`

```cpp
virtual Acad::ErrorStatus addGraph(
AcDbEvalGraph* pGraphToAdd, 
AcDbEvalIdMap*& idMap
);
```

**Description (verbatim):** Adds the nodes from one graph to another graph by moving the nodes and edges from the source graph to this graph. All nodes and edges in pGraphToAdd are moved to the destination graph. Returns Acad::eOk if successful.

| Parameter | Description (verbatim) |
|---|---|
| `pGraphToAdd` | Input graph containing the nodes and edges to add to this graph |
| `idMap` | Output pointer to map from old node ids within pGraphToAdd to their new ids within this graph |

#### `AcDbEvalGraph::addNode`

```cpp
virtual Acad::ErrorStatus addNode(
AcDbEvalExpr* pNode, 
AcDbEvalNodeId& id
);
```

**Description (verbatim):** Adds a node to the graph and returns the AcDbEvalNodeId of the newly added node. If the graph is database resident, the caller must call close() on pNode when it is no longer needed. Returns Acad::eOk if successful.

| Parameter | Description (verbatim) |
|---|---|
| `pNode` | Input pointer to the node to add to the graph |
| `id` | Output ID of newly added node |

#### `AcDbEvalGraph::createGraph` (2 overloads)

##### Overload: `AcDbEvalGraph::createGraph (AcDbDatabase, ACHAR)`

```cpp
static Acad::ErrorStatus createGraph(
AcDbDatabase* pDb, 
const ACHAR* pKey
);
```

**Description (verbatim):** Creates a graph for the database at the specified key. This method provides the ability to associate a graph with a database instead of with a specific object in the database. Returns Acad::eOk if successful.

| Parameter | Description (verbatim) |
|---|---|
| `pDb` | Input database from which to create the graph |
| `pKey` | Input key at which to associate the new graph |

##### Overload: `AcDbEvalGraph::createGraph (AcDbObject, ACHAR)`

```cpp
static Acad::ErrorStatus createGraph(
AcDbObject* pObj, 
const ACHAR* pKey
);
```

**Description (verbatim):** Creates a graph on the object at the supplied key. The object must be database resident. Returns Acad::eOk if successful. Returns eAlreadyInDb if the graph already exists on the object.

| Parameter | Description (verbatim) |
|---|---|
| `pObj` | Input object on which to create a graph |
| `pKey` | Input key at which to associate the new graph |

#### `AcDbEvalGraph::equals`

```cpp
virtual bool equals(
const AcDbEvalGraph* pOther
) const;
```

**Description (verbatim):** Determines whether two graphs are equal. Graph A is said to equal Graph B if A is a subgraph of B and B is a subgraph of A. See the isSubgraphOf() function for a definition of subgraphs. Returns true if this graph equals pOther . Otherwise, returns false.

| Parameter | Description (verbatim) |
|---|---|
| `pOther` | Input object to test for equality to this object |

#### `AcDbEvalGraph::evaluate` (3 overloads)

##### Overload: `AcDbEvalGraph::evaluate (AcDbEvalContext, AcDbEvalNodeIdArray)`

```cpp
virtual Acad::ErrorStatus evaluate(
const AcDbEvalContext* pContext, 
const AcDbEvalNodeIdArray* activatedNodes
) const;
```

**Description (verbatim):** Evaluates the class by traversing the graph and invoking AcDbEvalExpr::evaluate() on all of the visited nodes. Applications can activate and evaluate nodes in one step by providing a non-null activatedNodes array pointer. Otherwise, applications must activate one or more nodes in the graph by calling AcDbEvalGraph::activate() before calling this method. Returns Acad::eOk if succssful. If any of the calls to AcDbEvalExpr::evaluate() fail, the error returned by AcDbEvalExpr::evaluate() is returned by this method.

| Parameter | Description (verbatim) |
|---|---|
| `pContext` | Input pointer to an AcDbEvalContext object that is passed to each node as it is visited when calling its AcDbEvalExpr::evaluate() method |
| `activatedNodes` | Input array of AcDbEvalNodeId objects to activate when performing the evaluation |

##### Overload: `AcDbEvalGraph::evaluate (AcDbEvalContext)`

```cpp
virtual Acad::ErrorStatus evaluate(
const AcDbEvalContext* pContext
) const;
```

**Description (verbatim):** Evaluates the class by traversing the graph and invoking AcDbEvalExpr::evaluate() on all of the visited nodes. Applications must activate one or more nodes in the graph by calling AcDbEvalGraph::activate() before calling this method. Returns Acad::eOk if succssful. If any of the calls to AcDbEvalExpr::evaluate() fail, the error returned by AcDbEvalExpr::evaluate() is returned by this method.

| Parameter | Description (verbatim) |
|---|---|
| `pContext` | Input pointer to an AcDbEvalContext object that is passed to each node as it is visited when calling its AcDbEvalExpr::evaluate() method |

##### Overload: `AcDbEvalGraph::evaluate ()`

```cpp
virtual Acad::ErrorStatus evaluate() const;
```

**Description (verbatim):** Evaluates the class by traversing the graph and invoking AcDbEvalExpr::evaluate() on all of the visited nodes. Applications must activate one or more nodes in the graph by calling AcDbEvalGraph::activate() before calling this method. Returns Acad::eOk if successful. If any of the calls to AcDbEvalExpr::evaluate() fail, the error returned by AcDbEvalExpr::evaluate() is returned by this method.

#### `AcDbEvalGraph::getAllNodes`

```cpp
virtual Acad::ErrorStatus getAllNodes(
AcDbEvalNodeIdArray& nodes
) const;
```

**Description (verbatim):** Returns an array of all node IDs contained in the graph. Returns Acad::eOk if successful.

| Parameter | Description (verbatim) |
|---|---|
| `nodes` | Output array of AcDbEvalNodeId objects for all of the nodes in the graph |

#### `AcDbEvalGraph::getEdgeInfo`

```cpp
virtual Acad::ErrorStatus getEdgeInfo(
const AcDbEvalNodeId& nodeFrom, 
const AcDbEvalNodeId& nodeTo, 
AcDbEvalEdgeInfo& einfo
) const;
```

**Description (verbatim):** Returns information about an edge between two nodes in a graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with either of the specified IDs does not exist in the graph. Returns Acad::eGraphEdgeNotFound if no edge exists between the nodes.

| Parameter | Description (verbatim) |
|---|---|
| `nodeFrom` | Input AcDbEvalNodeId of the orginating node for the edge |
| `nodeTo` | Input AcDbEvalNodeId of the destination node for the edge |
| `einfo` | Output AcDbEvalEdgeInfo object describing the edge |

#### `AcDbEvalGraph::getGraph` (2 overloads)

##### Overload: `AcDbEvalGraph::getGraph (AcDbDatabase, ACHAR, AcDbEvalGraph, AcDb, OpenMode)`

```cpp
static Acad::ErrorStatus getGraph(
AcDbDatabase* pDb, 
const ACHAR* pKey, 
AcDbEvalGraph** pGraph, 
const AcDb::OpenMode mode
);
```

**Description (verbatim):** Retrieves the graph from the database. This method provides the ability to associate a graph with a database instead of with a specific object in the database. The calling application is responsible for calling close() on the returned graph when it is no longer needed. Returns Acad::eOk if successful. Returns Acad::eKeyNotFound if the specified key could not be found.

| Parameter | Description (verbatim) |
|---|---|
| `pDb` | Input database from which to retrieve the graph |
| `pKey` | Input key at which the graph is associated |
| `pGraph` | Output graph at the specified key, or null if the graph does not exist |
| `mode` | Input mode in which to open the graph |

##### Overload: `AcDbEvalGraph::getGraph (AcDbObject, ACHAR, AcDbEvalGraph, AcDb, OpenMode)`

```cpp
static Acad::ErrorStatus getGraph(
const AcDbObject* pObj, 
const ACHAR* pKey, 
AcDbEvalGraph** pGraph, 
const AcDb::OpenMode mode
);
```

**Description (verbatim):** Retrieves a graph, if one exists, from the supplied object with the requested open mode. The object must be database resident. The calling application is responsible for calling close() on the returned graph when it is no longer needed. Returns Acad::eOk if successful. Returns Acad::eKeyNotFound if the graph with the specified key name does not exist.

| Parameter | Description (verbatim) |
|---|---|
| `pObj` | Input object from which to retrieve the graph |
| `pKey` | Input key at which the graph is associated (multiple graphs can be associated with one object) |
| `pGraph` | Output resultant graph; *pGraph is set to null if not found |
| `mode` | Input mode in which to open the graph |

#### `AcDbEvalGraph::getIncomingEdges`

```cpp
virtual Acad::ErrorStatus getIncomingEdges(
const AcDbEvalNodeId& nodeId, 
AcDbEvalEdgeInfoArray& edges
) const;
```

**Description (verbatim):** Returns a list of incoming edges to a node in the graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `nodeId` | Input AcDbEvalNodeId of the node for which to retrieve incoming edges |
| `edges` | Output array of AcDbEvalEdgeInfo objects describing incoming edges for the node |

#### `AcDbEvalGraph::getIsActive`

```cpp
virtual Acad::ErrorStatus getIsActive(
const AcDbEvalNodeId& id, 
bool& bIsActive
) const;
```

**Description (verbatim):** Determines whether the specified node is activated in the graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `id` | Input AcDbEvalNodeId of a node in the graph |
| `bIsActive` | Output true if the specified node exists in the graph and is active, or false otherwise |

#### `AcDbEvalGraph::getNode`

```cpp
virtual Acad::ErrorStatus getNode(
const AcDbEvalNodeId& nodeId, 
AcDb::OpenMode mode, 
AcDbObject** ppNode
) const;
```

**Description (verbatim):** Opens a node in the graph given its node ID. Callers must call close() on the returned node pointer when it is no longer needed. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `nodeId` | Input AcDbEvalNodeId of the node to open |
| `mode` | Input AcDb::OpenMode with which to open the node |
| `ppNode` | Output pointer to the opened node |

#### `AcDbEvalGraph::getOutgoingEdges`

```cpp
virtual Acad::ErrorStatus getOutgoingEdges(
const AcDbEvalNodeId& nodeId, 
AcDbEvalEdgeInfoArray& edges
) const;
```

**Description (verbatim):** Returns a list of outgoing edges from a node in the graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `nodeId` | Input AcDbEvalNodeId of the node for which to retrieve outgoing edges |
| `edges` | Output array of AcDbEvalEdgeInfo objects describing outgoing edges for the node |

#### `AcDbEvalGraph::hasGraph` (2 overloads)

##### Overload: `AcDbEvalGraph::hasGraph (AcDbDatabase, ACHAR)`

```cpp
static bool hasGraph(
AcDbDatabase* pDb, 
const ACHAR* pKey
);
```

**Description (verbatim):** Determines if a graph exists at the supplied key in the database. Returns true if the graph exists.

| Parameter | Description (verbatim) |
|---|---|
| `pDb` | Input database from which to retrieve the graph |
| `pKey` | Input key at which the graph is registered in the database |

##### Overload: `AcDbEvalGraph::hasGraph (AcDbObject, ACHAR)`

```cpp
static bool hasGraph(
const AcDbObject* pObj, 
const ACHAR* pKey
);
```

**Description (verbatim):** Determines if a graph exists on the supplied object. The object must be database resident. Returns true if the graph exists.

| Parameter | Description (verbatim) |
|---|---|
| `pObj` | Input object from which to retrieve the graph |
| `pKey` | Input key at which the graph is associated (multiple graphs can be associated with one object) |

#### `AcDbEvalGraph::isSubgraphOf`

```cpp
virtual bool isSubgraphOf(
const AcDbEvalGraph* pOther
) const;
```

**Description (verbatim):** This function determines whether this graph is a subgraph of pOther . Before making this determination, the graph verifies that the node ID of each node in this graph matches that of a node in pOther . If any node ID in this graph is not found in pOther , this function returns false. If every node in this graph can be mapped to a node in pOther , the order and types of both the incoming and outgoing edges from each node in this graph must be found to be a subset of the incoming and outgoing edges for the corresponding mapped node in pOther . If this is true, this graph is a subgraph of pOther , and this function returns true. Otherwise, this function returns false.

| Parameter | Description (verbatim) |
|---|---|
| `pOther` | Input graph to be tested as a superset of this graph |

#### `AcDbEvalGraph::postInDatabase`

```cpp
virtual Acad::ErrorStatus postInDatabase(
AcDbObjectId& objId, 
AcDbDatabase* pDb
);
```

**Description (verbatim):** Adds this graph to the database, giving it a handle and an object ID. The new object ID is returned in the objId argument. All of this graph's nodes are added to the database and this graph is made their owner. This function does not establish ownership of this graph. Returns Acad::eOk if successful.

| Parameter | Description (verbatim) |
|---|---|
| `objId` | Output new object ID obtained by this function |
| `pDb` | Input destination database |

#### `AcDbEvalGraph::removeEdge`

```cpp
virtual Acad::ErrorStatus removeEdge(
const AcDbEvalNodeId& idFrom, 
const AcDbEvalNodeId& idTo
);
```

**Description (verbatim):** Removes an edge between two nodes in the graph. Returns Acad::eOk if successful. Returns Acad::eGraphEdgeNotFound if an edge between the nodes does not exist.

| Parameter | Description (verbatim) |
|---|---|
| `idFrom` | Input AcDbEvalNodeId of the node from which the edge originates |
| `idTo` | Input AcDbEvalNodeId of the node at which the edge terminates |

#### `AcDbEvalGraph::removeGraph` (2 overloads)

##### Overload: `AcDbEvalGraph::removeGraph (AcDbDatabase, ACHAR)`

```cpp
static Acad::ErrorStatus removeGraph(
AcDbDatabase* pDb, 
const ACHAR* pKey
);
```

**Description (verbatim):** Removes a graph at the specified key for the database. Returns Acad::eOk if successful. Returns Acad::eKeyNotFound if there is no graph with that key associated with the database.

| Parameter | Description (verbatim) |
|---|---|
| `pDb` | Input database from which to remove the graph |
| `pKey` | Input key at which the graph is associated |

##### Overload: `AcDbEvalGraph::removeGraph (AcDbObject, ACHAR)`

```cpp
static Acad::ErrorStatus removeGraph(
AcDbObject* pObj, 
const ACHAR* pKey
);
```

**Description (verbatim):** Removes the graph, if one exists, at the supplied key. The object must be database resident. Returns Acad::eOk if successful. Returns Acad::eKeyNotFound if the graph does not exist.

| Parameter | Description (verbatim) |
|---|---|
| `pObj` | Input object from which to remove the graph |
| `pKey` | Input key at which the graph is associated with the object |

#### `AcDbEvalGraph::removeNode` (2 overloads)

##### Overload: `AcDbEvalGraph::removeNode (AcDbEvalExpr)`

```cpp
virtual Acad::ErrorStatus removeNode(
AcDbEvalExpr* pNode
);
```

**Description (verbatim):** Removes a node from the graph. This method assumes that the object is already opened for read or write. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if the node does not exist in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `pNode` | Input node to remove from the graph |

##### Overload: `AcDbEvalGraph::removeNode (AcDbEvalNodeId)`

```cpp
virtual Acad::ErrorStatus removeNode(
const AcDbEvalNodeId& id
);
```

**Description (verbatim):** Removes a node from the graph. Returns Acad::eOk if successful. Returns Acad::eGraphNodeNotFound if a node with the specified ID does not exist in the graph.

| Parameter | Description (verbatim) |
|---|---|
| `id` | Input ID of the node to remove from the graph |

#### `AcDbEvalGraph::replaceGraph`

```cpp
static Acad::ErrorStatus replaceGraph(
AcDbObject* pObj, 
const ACHAR* pKey, 
AcDbObjectId grphId
);
```

**Description (verbatim):** Replaces a graph on the object. Returns Acad::eOk if successful, Acad::eWrongObjectType if wrong type of object (i.e. not an AcDbEvalGraph ).

| Parameter | Description (verbatim) |
|---|---|
| `pObj` | Input object on which to create a graph |
| `pKey` | Input key at which the graph is associated |
| `graphId` | Input ID of the graph that will be the replacement |

---

## AcDbEvalExpr

**File:** `dbeval.h`  
**C++:** `class AcDbEvalExpr : public AcDbObject;`  
**Class hierarchy:** `AcRxObject > AcGiDrawable > AcDbObject > AcDbEvalExpr > AcDbEvalConnectable`

**Official class description:**

> This class represents a single node in an AcDbEvalGraph . The node represents an action (or expression). The owning graph calls the node's evaluate() method when visiting the node during a traversal of the graph from within the graph's evaluate() method.

### Methods

Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):

| Method | Flags | Description (verbatim from index page) |
|---|---|---|
| `activated` | virtual | Called on graph-resident nodes that become active.This method is called by AcDbEvalGraph::activate() , or by AcDbEvalGraph::evaluate() when it is called with a non-null node activation arrray, on every node being activated by the call. |
| `addedToGraph` | virtual | Called when a node is added to a graph. AcDbEvalGraph::addNode() calls this method on the node after it has been added to the graph. |
| `adjacentEdgeAdded` | virtual | This notification callback function is called when an edge is added to the graph.Both versions of the AcDbEvalGraph::addEdge() function call this function on each of the nodes at either end of the edge being added. For example, when an invertible edge is added, four notifications occur. |
| `adjacentEdgeRemoved` | virtual | Called when an edge on the node is removed. AcDbEvalGraph::removeEdge() calls this method on the nodes at either end of the edge being removed. |
| `adjacentNodeRemoved` | virtual | Called when a node with a shared edge to this node is removed from the graph. AcDbEvalGraph::removeNode() calls this method on nodes that share edges with the node being removed. |
| `copiedIntoGraph` | virtual | Called when a node is finished being inserted because of a copy operation from one graph to another. AcDbEvalGraph::copyFrom() calls this method on the nodes that are copies of the argument graph nodes. Because all the nodes from the argument graph as well as their edges are copied, the graph structure in this becomes isomorphic to the graph structure in the source graph. |
| `equals` | virtual | Determines if two AcDbEvalExpr objects are equal. The default implementation simply checks that the concrete class types are the same, as in the following code: this->isA() == pOther->isA() |
| `evaluate` | virtual | Causes the expression represented by the node to be evaluated. Called for a graph-resident node when the node is visited during a call to AcDbEvalGraph::evaluate() .The default implementation does nothing and returns Acad::eOk . Any nonsuccessful result terminates the graph traversal. |
| `getGraph` | — | Returns a pointer to the graph owning the node.The caller is responsible for calling close() on the returned graph pointer.Returns Acad::eOk if successful. Returns Acad::eNulNodeId if the node is not graph resident. |
| `graphEvalAbort` | virtual | Called on graph-resident nodes after aborting the traversal of the graph in a call to AcDbEvalGraph::evaluate() .This method is called by AcDbEvalGraph::evaluate() for nodes that were in the traversal list of an aborted graph evaluation. |
| `graphEvalEnd` | virtual | Called on graph-resident nodes after traversing the graph in a call to AcDbEvalGraph::evaluate() .This method is called by AcDbEvalGraph::evaluate() for nodes that were visited during the traversal of the graph. |
| `graphEvalStart` | virtual | Called on graph-resident nodes before traversing the graph in a call to AcDbEvalGraph::evaluate() .This method is called by AcDbEvalGraph::evaluate() for nodes that will be traversed during the graph evaluation. |
| `isActivatable` | virtual | Returns a value indicating whether this expression can be activated. The base class version always returns true. |
| `movedFromGraph` | virtual | Called when a node is about to be moved from pFromGraph to another graph. AcDbEvalGraph::addGraph() calls this method on the node just before it is added to the destination graph. |
| `movedIntoGraph` | virtual | Called when a node is finished being moved into pIntoGraph from another graph. AcDbEvalGraph::addGraph() calls this method on the node just after it is added to the destination graph. Because addGraph() copies a disjoint subgraph into the destination graph, the newly added subgraph is isomorphic to the source graph in the addGraph operation. |
| `nodeId` | — | Returns the ID of the node.When a node is added to a graph, it is assigned an ID that is unique among all nodes in the graph. If the node is not graph resident, the ID is AcDbEvalGraph::kNullNodeId .Returns the ID of the node if it is graph resident. Returns AcDbGraph::kNullId if the node is not graph resident. |
| `postInDatabase` | virtual | This function adds this object to the database, giving it a handle and an object ID. The new object ID is returned in the objId argument. This function does not establish ownership of this object. Subclasses with child objects should override this function to add them to the database at this time and establish ownership.Returns Acad::eOk if successful. |
| `remappedNodeIds` | virtual | AcDbEvalGraph::addGraph() calls this method on each node in the to-be-added graph just after all nodes have been added to the destination graph. The default behavior is to do nothing. Some subclasses of AcDbEvalExpr should override this function to update their references to other nodes. Nodes should not alter the map.This function is called exactly once for each node in a graph after all node IDs in a graph are changed. |
| `removedFromGraph` | virtual | Called when a node is removed from a graph. AcDbEvalGraph::removeNode() calls this method on the node after it has been removed from the graph. |
| `value` | — | The value of the variant node.The value is usually updated during the AcDbEvalExpr::evaluate() call. The default value is uninitialized ( AcDbEvalVariant::Type::kNone ).Returns an AcDbEvalVariant representing the value of the evaluated expression. |

Full detail for every method (verbatim C++ signature, description, parameters):

#### `AcDbEvalExpr::activated`

```cpp
virtual void activated(
AcDbEvalNodeIdArray& argumentActiveList
);
```

**Description (verbatim):** Called on graph-resident nodes that become active. This method is called by AcDbEvalGraph::activate() , or by AcDbEvalGraph::evaluate() when it is called with a non-null node activation arrray, on every node being activated by the call.

| Parameter | Description (verbatim) |
|---|---|
| `argumentActiveList` | Input array of AcDbEvalNodeId objects of the nodes being activated in the graph |

#### `AcDbEvalExpr::addedToGraph`

```cpp
virtual void addedToGraph(
AcDbEvalGraph* pGraph
);
```

**Description (verbatim):** Called when a node is added to a graph. AcDbEvalGraph::addNode() calls this method on the node after it has been added to the graph.

| Parameter | Description (verbatim) |
|---|---|
| `pGraph` | Input pointer to the graph to which the node is added |

#### `AcDbEvalExpr::adjacentEdgeAdded`

```cpp
virtual void adjacentEdgeAdded(
const AcDbEvalNodeId& fromId, 
const AcDbEvalNodeId& toId, 
bool bIsInvertible
);
```

**Description (verbatim):** This notification callback function is called when an edge is added to the graph. Both versions of the AcDbEvalGraph::addEdge() function call this function on each of the nodes at either end of the edge being added. For example, when an invertible edge is added, four notifications occur.

| Parameter | Description (verbatim) |
|---|---|
| `fromId` | Output the "from" AcDbEvalNodeId of the node whose shared edge is being added |
| `toId` | Output the "to" AcDbEvalNodeId of the node whose shared edge is being added |
| `bIsInvertible` | Output value indicating whether the edge is invertible |

#### `AcDbEvalExpr::adjacentEdgeRemoved`

```cpp
virtual void adjacentEdgeRemoved(
const AcDbEvalNodeId& adjEdgeNodeId
);
```

**Description (verbatim):** Called when an edge on the node is removed. AcDbEvalGraph::removeEdge() calls this method on the nodes at either end of the edge being removed.

| Parameter | Description (verbatim) |
|---|---|
| `adjEdgeNodeId` | Input AcDbEvalNodeId of the node whose shared edge is being removed |

#### `AcDbEvalExpr::adjacentNodeRemoved`

```cpp
virtual void adjacentNodeRemoved(
const AcDbEvalNodeId& adjNodeId
);
```

**Description (verbatim):** Called when a node with a shared edge to this node is removed from the graph. AcDbEvalGraph::removeNode() calls this method on nodes that share edges with the node being removed.

| Parameter | Description (verbatim) |
|---|---|
| `adjNodeId` | Input AcDbEvalNodeId of the removed node |

#### `AcDbEvalExpr::copiedIntoGraph`

```cpp
virtual void copiedIntoGraph(
AcDbEvalGraph* pIntoGraph
);
```

**Description (verbatim):** Called when a node is finished being inserted because of a copy operation from one graph to another. AcDbEvalGraph::copyFrom() calls this method on the nodes that are copies of the argument graph nodes. Because all the nodes from the argument graph as well as their edges are copied, the graph structure in this becomes isomorphic to the graph structure in the source graph.

| Parameter | Description (verbatim) |
|---|---|
| `pIntoGraph` | Graph into which the node is being added |

#### `AcDbEvalExpr::equals`

```cpp
virtual bool equals(
const AcDbEvalExpr* pOther
) const;
```

**Description (verbatim):** Determines if two AcDbEvalExpr objects are equal. The default implementation simply checks that the concrete class types are the same, as in the following code: this->isA() == pOther->isA()

| Parameter | Description (verbatim) |
|---|---|
| `pOther` | Input object to test for equality to this object |

#### `AcDbEvalExpr::evaluate`

```cpp
virtual Acad::ErrorStatus evaluate(
const AcDbEvalContext* ctxt
);
```

**Description (verbatim):** Causes the expression represented by the node to be evaluated. Called for a graph-resident node when the node is visited during a call to AcDbEvalGraph::evaluate() . The default implementation does nothing and returns Acad::eOk . Any nonsuccessful result terminates the graph traversal.

| Parameter | Description (verbatim) |
|---|---|
| `ctxt` | Input pointer to the AcDbEvalContext object used for the graph evaluation; may be null |

#### `AcDbEvalExpr::getGraph`

```cpp
Acad::ErrorStatus getGraph(
AcDbEvalGraph** pGraph, 
AcDb::OpenMode mode
) const;
```

**Description (verbatim):** Returns a pointer to the graph owning the node. The caller is responsible for calling close() on the returned graph pointer. Returns Acad::eOk if successful. Returns Acad::eNulNodeId if the node is not graph resident.

| Parameter | Description (verbatim) |
|---|---|
| `pGraph` | Output pointer to the graph owning the node |
| `mode` | Input AcDb::OpenMode in which to open the owning graph |

#### `AcDbEvalExpr::graphEvalAbort`

```cpp
virtual void graphEvalAbort(
bool bNodeIsActive
);
```

**Description (verbatim):** Called on graph-resident nodes after aborting the traversal of the graph in a call to AcDbEvalGraph::evaluate() . This method is called by AcDbEvalGraph::evaluate() for nodes that were in the traversal list of an aborted graph evaluation.

| Parameter | Description (verbatim) |
|---|---|
| `bNodeIsActive` | True if the node has been activated for the traversal |

#### `AcDbEvalExpr::graphEvalEnd`

```cpp
virtual void graphEvalEnd(
bool bNodeIsActive
);
```

**Description (verbatim):** Called on graph-resident nodes after traversing the graph in a call to AcDbEvalGraph::evaluate() . This method is called by AcDbEvalGraph::evaluate() for nodes that were visited during the traversal of the graph.

| Parameter | Description (verbatim) |
|---|---|
| `bNodeIsActive` | True if the node has been activated for the traversal |

#### `AcDbEvalExpr::graphEvalStart`

```cpp
virtual void graphEvalStart(
bool bNodeIsActive
);
```

**Description (verbatim):** Called on graph-resident nodes before traversing the graph in a call to AcDbEvalGraph::evaluate() . This method is called by AcDbEvalGraph::evaluate() for nodes that will be traversed during the graph evaluation.

| Parameter | Description (verbatim) |
|---|---|
| `bNodeIsActive` | True if the node has been activated for the traversal |

#### `AcDbEvalExpr::isActivatable`

```cpp
virtual bool isActivatable();
```

**Description (verbatim):** Returns a value indicating whether this expression can be activated. The base class version always returns true.

#### `AcDbEvalExpr::movedFromGraph`

```cpp
virtual void movedFromGraph(
AcDbEvalGraph* pFromGraph
);
```

**Description (verbatim):** Called when a node is about to be moved from pFromGraph to another graph. AcDbEvalGraph::addGraph() calls this method on the node just before it is added to the destination graph.

| Parameter | Description (verbatim) |
|---|---|
| `pFromGraph` | Graph from which the node is being transplanted |

#### `AcDbEvalExpr::movedIntoGraph`

```cpp
virtual void movedIntoGraph(
AcDbEvalGraph* pIntoGraph
);
```

**Description (verbatim):** Called when a node is finished being moved into pIntoGraph from another graph. AcDbEvalGraph::addGraph() calls this method on the node just after it is added to the destination graph. Because addGraph() copies a disjoint subgraph into the destination graph, the newly added subgraph is isomorphic to the source graph in the addGraph operation.

| Parameter | Description (verbatim) |
|---|---|
| `pIntoGraph` | Graph into which the node is being transplanted |

#### `AcDbEvalExpr::nodeId`

```cpp
AcDbEvalNodeId nodeId() const;
```

**Description (verbatim):** Returns the ID of the node. When a node is added to a graph, it is assigned an ID that is unique among all nodes in the graph. If the node is not graph resident, the ID is AcDbEvalGraph::kNullNodeId . Returns the ID of the node if it is graph resident. Returns AcDbGraph::kNullId if the node is not graph resident.

#### `AcDbEvalExpr::postInDatabase`

```cpp
virtual Acad::ErrorStatus postInDatabase(
AcDbObjectId& objId, 
AcDbDatabase* pDb
);
```

**Description (verbatim):** This function adds this object to the database, giving it a handle and an object ID. The new object ID is returned in the objId argument. This function does not establish ownership of this object. Subclasses with child objects should override this function to add them to the database at this time and establish ownership. Returns Acad::eOk if successful.

| Parameter | Description (verbatim) |
|---|---|
| `objId` | Output new object ID obtained by this function |
| `pDb` | Input destination database |

#### `AcDbEvalExpr::remappedNodeIds`

```cpp
virtual void remappedNodeIds(
AcDbEvalIdMap& idMap
);
```

**Description (verbatim):** AcDbEvalGraph::addGraph() calls this method on each node in the to-be-added graph just after all nodes have been added to the destination graph. The default behavior is to do nothing. Some subclasses of AcDbEvalExpr should override this function to update their references to other nodes. Nodes should not alter the map. This function is called exactly once for each node in a graph after all node IDs in a graph are changed.

| Parameter | Description (verbatim) |
|---|---|
| `idMap` | Map from old node IDs within pGraphToAdd to their new IDs within this graph. Should be empty initially and be the size of the number of nodes on successful return. |

#### `AcDbEvalExpr::removedFromGraph`

```cpp
virtual void removedFromGraph(
AcDbEvalGraph* pGraph
);
```

**Description (verbatim):** Called when a node is removed from a graph. AcDbEvalGraph::removeNode() calls this method on the node after it has been removed from the graph.

| Parameter | Description (verbatim) |
|---|---|
| `pGraph` | Pointer to the graph from which the node is removed |

#### `AcDbEvalExpr::value`

```cpp
AcDbEvalVariant value() const;
```

**Description (verbatim):** The value of the variant node. The value is usually updated during the AcDbEvalExpr::evaluate() call. The default value is uninitialized ( AcDbEvalVariant::Type::kNone ). Returns an AcDbEvalVariant representing the value of the evaluated expression.

---

## AcDbEvalContext

**File:** `dbeval.h`  
**C++:** `class AcDbEvalContext : public AcRxObject, public AcHeapOperators;`  
**Class hierarchy:** `AcHeapOperators > AcRxObject > AcDbEvalContext`

**Official class description:**

> This class implements a simple container for application data that can be used during the evaluation of an AcDbEvalGraph . The graph passes any AcDbEvalContext object supplied in a call to AcDbEvalGraph::evaluate() to each node in the graph when calling AcDbEvalExpr::evaluate() during the ensuing traversal. Graph client applications typically use the context to store application-specific data used by custom nodes during their evaluation.

### Methods

Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):

| Method | Flags | Description (verbatim from index page) |
|---|---|---|
| `getAt` | virtual | Returns an AcDbEvalContextPair stored in the context.The pair passed in should be initialized with the desired key to return in the context. If the key exists, pair is updated with the value from the context.Returns Acad::eOk if successful. Returns Acad::eKeyNotFound if no entry exists with the specified key. |
| `insertAt` | virtual | Inserts an AcDbEvalContextPair into the context.If an AcDbEvalContextPair with the specified key already exists, it is replaced with the new pair. |
| `newIterator` | virtual | Returns a new AcDbEvalContextIterator for the context.Returns a new AcDbEvalContextIterator . Callers must delete the iterator when it is no longer needed by calling delete() . |
| `removeAt` | virtual | Removes an AcDbEvalContextPair from the context. |

Full detail for every method (verbatim C++ signature, description, parameters):

#### `AcDbEvalContext::getAt`

```cpp
virtual Acad::ErrorStatus getAt(
AcDbEvalContextPair& pair
) const;
```

**Description (verbatim):** Returns an AcDbEvalContextPair stored in the context. The pair passed in should be initialized with the desired key to return in the context. If the key exists, pair is updated with the value from the context. Returns Acad::eOk if successful. Returns Acad::eKeyNotFound if no entry exists with the specified key.

| Parameter | Description (verbatim) |
|---|---|
| `pair` | Input/output AcDbEvalContextPair to insert into the context |

#### `AcDbEvalContext::insertAt`

```cpp
virtual void insertAt(
const AcDbEvalContextPair& pair
);
```

**Description (verbatim):** Inserts an AcDbEvalContextPair into the context. If an AcDbEvalContextPair with the specified key already exists, it is replaced with the new pair.

| Parameter | Description (verbatim) |
|---|---|
| `pair` | Input AcDbEvalContextPair to insert into the context |

#### `AcDbEvalContext::newIterator`

```cpp
virtual AcDbEvalContextIterator* newIterator() const;
```

**Description (verbatim):** Returns a new AcDbEvalContextIterator for the context. Returns a new AcDbEvalContextIterator . Callers must delete the iterator when it is no longer needed by calling delete() .

#### `AcDbEvalContext::removeAt`

```cpp
virtual void removeAt(
const ACHAR* szKey
);
```

**Description (verbatim):** Removes an AcDbEvalContextPair from the context.

| Parameter | Description (verbatim) |
|---|---|
| `szKey` | Input key of the AcDbEvalContextPair to remove from the context |

---

## AcDbEvalIdMap

**File:** `dbeval.h`  
**C++:** `class AcDbEvalIdMap;`  
**Class hierarchy:** `AcDbEvalIdMap`

**Official class description:**

> This class is used by AcDbEvalExpr::remappedNodeIds() to map old node IDs to new node IDs.

### Methods

Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):

| Method | Flags | Description (verbatim from index page) |
|---|---|---|
| `find` | abstract | Find the node ID that key maps to. |
| `kill` | abstract | Delete this map. |

Full detail for every method (verbatim C++ signature, description, parameters):

#### `AcDbEvalIdMap::find`

```cpp
virtual AcDbEvalNodeId find(
const AcDbEvalNodeId& key
) = 0;
```

**Description (verbatim):** Find the node ID that key maps to.

| Parameter | Description (verbatim) |
|---|---|
| `key` | Input node ID |

#### `AcDbEvalIdMap::kill`

```cpp
virtual void kill() = 0;
```

**Description (verbatim):** Delete this map.

---

## AcDbEvalVariant

**File:** `dbeval.h`  
**C++:** `class AcDbEvalVariant : public resbuf, public AcRxObject;`  
**Class hierarchy:** `AcRxObject > resbuf > AcDbEvalVariant`

**Official class description:**

> This class provides a lightweight wrapper for a resbuf structure. It provides typed constructors and overloaded assignment operators to facilitiate assigning values to the underlying data. AcDbEvalExpr objects return instances of this class for the result of the expressions.
> The class manages the copying of strings by calling acutNewString() to copy strings. Linked lists of resbufs are not directly supported, but if an AcDbEvalVariant contains a linked resbuf chain the destructor frees the entire chain using acutRelRb() .

### Constructors

From the official constructor overload-list page:

| Constructor (official overload list) | Description (verbatim) |
|---|---|
| `AcDbEvalVariant::AcDbEvalVariant ()` | Default constructor.Allocates the resbuf and initializes the variant type to AcDbEvalVariant::kNone . |
| `AcDbEvalVariant::AcDbEvalVariant (AcDbEvalVariant&amp;)` | Copy constructor. |
| `AcDbEvalVariant::AcDbEvalVariant (AcDbEvalVariant&amp;&amp;)` | Move constructor. |
| `AcDbEvalVariant::AcDbEvalVariant (AcDbEvalVariant*)` | Copy constructor. |
| `AcDbEvalVariant::AcDbEvalVariant (AcDbObjectId&amp;)` | Constructs an AcDbEvalVariant wrapping an AcDbObjectId .The variant type is set to AcDbEvalVariant::kOldId . |
| `AcDbEvalVariant::AcDbEvalVariant (AcGePoint2d&amp;)` | Constructs an AcDbEvalVariant wrapping a 2D point.The variant type is set to AcDbEvalVariant::kPoint2d . |
| `AcDbEvalVariant::AcDbEvalVariant (AcGePoint3d&amp;)` | Constructs an AcDbEvalVariant wrapping a 3D point.The variant type is set to AcDbEvalVariant::kPoint3d . |
| `AcDbEvalVariant::AcDbEvalVariant (ACHAR*)` | Constructs an AcDbEvalVariant wrapping a string value.The variant type is set to AcDbEvalVariant::kString . |
| `AcDbEvalVariant::AcDbEvalVariant (Adesk::Int32)` | Constructs an AcDbEvalVariant wrapping a long value.The variant type is set to AcDbEvalVariant::kLong . |
| `AcDbEvalVariant::AcDbEvalVariant (double)` | Constructs an AcDbEvalVariant wrapping a double value.The variant type is set to AcDbEvalVariant::kDouble . |
| `AcDbEvalVariant::AcDbEvalVariant (resbuf&amp;)` | Constructs an AcDbEvalVariant from a resbuf.The variant type is set to rb.restype . |
| `AcDbEvalVariant::AcDbEvalVariant (short)` | Constructs an AcDbEvalVariant wrapping a short integer value.The variant type is set to AcDbEvalVariant::kShort . |

Full detail pages (verbatim signatures + parameters):

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant();
```

**Description (verbatim):** Default constructor. Allocates the resbuf and initializes the variant type to AcDbEvalVariant::kNone .

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
const ACHAR* szVal
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping a string value. The variant type is set to AcDbEvalVariant::kString .

| Parameter | Description (verbatim) |
|---|---|
| `szVal` | Input value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
const AcDbEvalVariant& other
);
```

**Description (verbatim):** Copy constructor.

| Parameter | Description (verbatim) |
|---|---|
| `other` | Object to copy from |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
const AcDbEvalVariant* pOther
);
```

**Description (verbatim):** Copy constructor.

| Parameter | Description (verbatim) |
|---|---|
| `pOther` | Input pointer to the object to copy from |

#### `AcDbEvalVariant::AcDbEvalVariant (AcDbEvalVariant&amp;&amp;) Constructor`

```cpp
ACDBCORE2D_PORT AcDbEvalVariant(
AcDbEvalVariant&& other
);
```

**Description (verbatim):** Move constructor.

| Parameter | Description (verbatim) |
|---|---|
| `other` | Object to move from |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
const AcDbObjectId& id
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping an AcDbObjectId . The variant type is set to AcDbEvalVariant::kOldId .

| Parameter | Description (verbatim) |
|---|---|
| `id` | Input value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
const AcGePoint2d& pt
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping a 2D point. The variant type is set to AcDbEvalVariant::kPoint2d .

| Parameter | Description (verbatim) |
|---|---|
| `pt` | Input value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
const AcGePoint3d& pt
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping a 3D point. The variant type is set to AcDbEvalVariant::kPoint3d .

| Parameter | Description (verbatim) |
|---|---|
| `pt` | Input value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
Adesk::Int32 lVal
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping a long value. The variant type is set to AcDbEvalVariant::kLong .

| Parameter | Description (verbatim) |
|---|---|
| `lVal` | Input value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
double dVal
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping a double value. The variant type is set to AcDbEvalVariant::kDouble .

| Parameter | Description (verbatim) |
|---|---|
| `dVal` | Input value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant (resbuf&amp;) Constructor`

```cpp
AcDbEvalVariant(
const resbuf& rb
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant from a resbuf. The variant type is set to rb.restype .

| Parameter | Description (verbatim) |
|---|---|
| `rb` | Value to assign to the object |

#### `AcDbEvalVariant::AcDbEvalVariant`

```cpp
AcDbEvalVariant(
short iVal
);
```

**Description (verbatim):** Constructs an AcDbEvalVariant wrapping a short integer value. The variant type is set to AcDbEvalVariant::kShort .

| Parameter | Description (verbatim) |
|---|---|
| `iVal` | Input value to assign to the object |

### Operators

One operator group (`=`) is documented, with 10 overloads (from the official overload-list page):

| Operator (official overload list) | Description (verbatim) |
|---|---|
| `AcDbEvalVariant::= (AcDbEvalVariant&amp;)` | Assigns an AcDbEvalVariant to the value stored in another AcDbEvalVariant .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (AcDbEvalVariant&amp;&amp;)` | Moves an AcDbEvalVariant to the value stored in another AcDbEvalVariant . |
| `AcDbEvalVariant::= (AcDbObjectId&amp;)` | Assigns an AcDbEvalVariant to the value of an AcDbObjectId .The variant type is set to AcDbEvalVariant::kOldId .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (AcGePoint2d&amp;)` | Assigns an AcDbEvalVariant to a 2D point value.The variant type is set to AcDbEvalVariant::kPoint2d .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (AcGePoint3d&amp;)` | Assigns an AcDbEvalVariant to a 3D point value.The variant type is set to AcDbEvalVariant::kPoint3d .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (ACHAR*)` | Assigns an AcDbEvalVariant to a character string.The variant type is set to AcDbEvalVariant::kString .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (Adesk::Int32)` | Assigns an AcDbEvalVariant to a long value.The variant type is set to AcDbEvalVariant::kLong .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (double)` | Assigns an AcDbEvalVariant to a double value.The variant type is set to AcDbEvalVariant::kReal .Returns a reference to the updated variant. |
| `AcDbEvalVariant::= (resbuf&amp;)` | Assigns an AcDbEvalVariant to a resbuf.The variant type is set to rb.restype . |
| `AcDbEvalVariant::= (short)` | Assigns an AcDbEvalVariant to a short value.The variant type is set to AcDbEvalVariant::kShort .Returns a reference to the updated variant. |

> Note (verbatim inconsistency in the official docs): the `double` **constructor** sets the variant
> type to `AcDbEvalVariant::kDouble`, while the `double` **assignment operator** sets it to
> `AcDbEvalVariant::kReal` — both as written in the official 2025 documentation.

### Methods

Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):

| Method | Flags | Description (verbatim from index page) |
|---|---|---|
| `clear` | — | Clears the contents of the AcDbEvalVariant and frees any allocated memory, including resbuf chains descending from this AcDbEvalVariant . Changes the AcDbEvalVariant::restype to AcDbEvalVariant::kNone . |
| `copyFrom` | — | Copies the value and data from a source AcDbEvalVariant object into this object.Returns Acad::eOk if successful. Returns Acad::eWrongObjectType if pObject is not an AcDbEvalVariant . |
| `fromAcRxValue` | — | This is fromAcRxValue, a member of class AcDbEvalVariant. |
| `init` | — | Initializes the contents of the AcDbEvalVariant .This protected method is called by constructor overloads to initialize the base resbuf memory. |
| `toAcRxValue` | — | This is toAcRxValue, a member of class AcDbEvalVariant. |

Full detail for every method (verbatim C++ signature, description, parameters):

#### `AcDbEvalVariant::clear`

```cpp
void clear();
```

**Description (verbatim):** Clears the contents of the AcDbEvalVariant and frees any allocated memory, including resbuf chains descending from this AcDbEvalVariant . Changes the AcDbEvalVariant::restype to AcDbEvalVariant::kNone .

#### `AcDbEvalVariant::copyFrom`

```cpp
Acad::ErrorStatus copyFrom(
const AcRxObject* pOther
) override;
```

**Description (verbatim):** Copies the value and data from a source AcDbEvalVariant object into this object. Returns Acad::eOk if successful. Returns Acad::eWrongObjectType if pObject is not an AcDbEvalVariant .

| Parameter | Description (verbatim) |
|---|---|
| `pOther` | Input pointer to the AcDbEvalVariant from which to copy |

#### `AcDbEvalVariant::fromAcRxValue`

```cpp
Acad::ErrorStatus fromAcRxValue(
const AcRxValue& value
);
```

**Description (verbatim):** This is fromAcRxValue, a member of class AcDbEvalVariant.

#### `AcDbEvalVariant::init`

```cpp
void init();
```

**Description (verbatim):** Initializes the contents of the AcDbEvalVariant . This protected method is called by constructor overloads to initialize the base resbuf memory.

#### `AcDbEvalVariant::toAcRxValue`

```cpp
Acad::ErrorStatus toAcRxValue(
const AcRxValueType& type, 
AcRxValue& value
) const;
```

**Description (verbatim):** This is toAcRxValue, a member of class AcDbEvalVariant.

---

## AcDbEvalContextPair

**File:** `dbeval.h`  
**C++:** `class AcDbEvalContextPair : public AcHeapOperators;`  
**Class hierarchy:** `AcHeapOperators > AcDbEvalContextPair`

**Official class description:**

> This class represents a single entry in an AcDbEvalContext container.
> This is a simple key-value pair stored in an AcDbEvalContext . The values are void pointers. Neither this class nor the AcDbEvalContext class is responsible for managing the memory allocated to the values stored in the context.

### Constructors

From the official constructor overload-list page:

| Constructor (official overload list) | Description (verbatim) |
|---|---|
| `AcDbEvalContextPair::AcDbEvalContextPair ()` | Default constructor. |
| `AcDbEvalContextPair::AcDbEvalContextPair (ACHAR*, void *)` | Constructor. |

Full detail pages (verbatim signatures + parameters):

#### `AcDbEvalContextPair::AcDbEvalContextPair`

```cpp
AcDbEvalContextPair();
```

**Description (verbatim):** Default constructor.

#### `AcDbEvalContextPair::AcDbEvalContextPair`

```cpp
AcDbEvalContextPair(
const ACHAR* szKey, 
void * pValue
);
```

**Description (verbatim):** Constructor.

| Parameter | Description (verbatim) |
|---|---|
| `szKey` | Input key used to look up the pair in an AcDbEvalContext |
| `pValue` | Input pointer to the data stored in the context pair |

### Methods

Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):

| Method | Flags | Description (verbatim from index page) |
|---|---|---|
| `key` | — | Returns the key used to look up the pair in an AcDbEvalContext . |
| `setKey` | — | Sets the key used to look up the pair in an AcDbEvalContext . |
| `setValue` | — | Sets the values stored with the pair in an AcDbEvalContext .The memory pointed to by pValue must be allocated and freed by the caller. |
| `value` | — | Returns the values stored with the pair in an AcDbEvalContext . |

Full detail for every method (verbatim C++ signature, description, parameters):

#### `AcDbEvalContextPair::key`

```cpp
const ACHAR* key() const;
```

**Description (verbatim):** Returns the key used to look up the pair in an AcDbEvalContext .

#### `AcDbEvalContextPair::setKey`

```cpp
void setKey(
const ACHAR* szKey
);
```

**Description (verbatim):** Sets the key used to look up the pair in an AcDbEvalContext .

| Parameter | Description (verbatim) |
|---|---|
| `szKey` | Input key used to look up the pair in an AcDbEvalContext |

#### `AcDbEvalContextPair::setValue`

```cpp
void setValue(
void* pValue
);
```

**Description (verbatim):** Sets the values stored with the pair in an AcDbEvalContext . The memory pointed to by pValue must be allocated and freed by the caller.

| Parameter | Description (verbatim) |
|---|---|
| `pValue` | Value to store in the AcDbEvalContextPair |

#### `AcDbEvalContextPair::value`

```cpp
void* value() const;
```

**Description (verbatim):** Returns the values stored with the pair in an AcDbEvalContext .

---

## Sources used

| Source | URL | Result |
|---|---|---|
| help.autodesk.com OARX 2025 JS app (direct) | https://help.autodesk.com/view/OARX/2025/ENU/?guid=OARX-RefGuide-__MEMBERTYPE_Methods_AcDbEvalGraph | **JS shell only** — returns a 6.6 KB `Help` bootstrap page; no content |
| help.autodesk.com athena app config | https://help.autodesk.com/view/OARX/2025/ENU/config/config.json , https://help.autodesk.com/view/athena/config/common.json , https://help.autodesk.com/view/athena/modules/athena-core.js | **Worked** — revealed the content API: beehive service URIs in athena-core.js (`serviceUris.guid` = `/community/service/rest/cloudhelp/resource/cloudhelpchannel/bookmark/` on `https://beehive.autodesk.com`) |
| Autodesk beehive REST API (guid→content URL) | https://beehive.autodesk.com/community/service/rest/cloudhelp/resource/cloudhelpchannel/bookmark/?p=OARX&v=2025&l=ENU&guid=... | **Worked with browser headers** (plain curl → 403; with Origin/Referer/UA headers → returns the real content URL). Maps each guid to `https://help.autodesk.com/cloudhelp/2025/ENU/OARX-RefGuide/files/<topic>.html` |
| Official OARX 2025 static content (primary source) | https://help.autodesk.com/cloudhelp/2025/ENU/OARX-RefGuide/files/OARX-RefGuide-*.html | **Worked — primary source for everything above**: 6 class overviews, 6 methods-index pages, 66 method/constructor/operator detail pages, NodeId enum, constructor & operator overload lists. All fetched 200 OK, zero 404s |
| Official OARX 2024 (version check) | https://help.autodesk.com/cloudhelp/2024/ENU/OARX-RefGuide/files/OARX-RefGuide-__MEMBERTYPE_Methods_<Class>.html | **Worked** — byte-identical to 2025 except version metadata |
| Official OARXMAC 2024 (AutoCAD for Mac, .NET) | https://help.autodesk.com/cloudhelp/2024/ENU/OARXMAC-RefGuide/files/OARXMAC-RefGuide-__MEMBERTYPE_Methods_<Class>.html | **Worked** — identical method sets for all six classes (same overloads) |
| Wayback Machine (CDX + id_ captures) | http://web.archive.org/cdx/search/cdx?url=help.autodesk.com/view/OARX/2023/ENU/* , http://web.archive.org/web/2024id_/https://help.autodesk.com/view/OARX/2024/ENU/?guid=... | **Not needed / not archived** — the 2024 guid URL was not archived; direct cloudhelp access made Wayback unnecessary |
| Graebert FRX SDK docs | https://docs.dev.graebert.com/html/2025.0.1/frx/files.html | **Reachable** — lists AcDbEvalConnectable.h, AcDbEvalContext.h, AcDbEvalContextIterator.h, AcDbEvalContextPair.h, AcDbEvalEdgeInfo.h, AcDbEvalExpr.h, AcDbEvalGraph.h, AcDbEvalVariant.h and dbeval.h. (Secondary source; not needed since the official docs provided the complete API. Note: FRX is an ODA-compatible SDK, so its copies of these classes are ODA's, not Autodesk's.) |
| SmartObjectARX GitHub repo | https://github.com/kevinzhwl/SmartObjectARX (tree: `inc/AcDbEval*`) | **Dead end for header content** — the repo's `inc/AcDbEvalGraph`, `inc/AcDbEvalExpr`, etc. are 19-byte stub files containing only `#include "dbeval.h"`; the real headers live in the ObjectARX SDK, not in the repo |

### Notes on gaps / caveats

- **No properties pages exist** for any of the six classes in the official 2025 docs
  (`__MEMBERTYPE_Properties_*` and `__MEMBERTYPE_Constructors_*` URLs return 404). The only members
  documented are the methods/constructors/operators/enumeration listed above. `AcDbEvalVariant`'s
  `restype` member is referenced in method descriptions (e.g. `clear()`) but no standalone property page
  for it exists in the official docs.
- **Placeholder descriptions in the official docs**: `AcDbEvalVariant::fromAcRxValue` and
  `AcDbEvalVariant::toAcRxValue` are documented verbatim only as "This is fromAcRxValue, a member of
  class AcDbEvalVariant." / "This is toAcRxValue, a member of class AcDbEvalVariant." — the official
  2025 docs contain no real description for these two methods (recorded verbatim, not invented).
- **Verbatim doc typos preserved**: e.g. `AcDbEvalGraph::evaluate` overloads say "Returns Acad::eOk if
  **succssful**" in two of the three overloads; `AcDbEvalExpr::activated` docs say "activation **arrray**";
  `AcDbEvalExpr::nodeId` docs say "Returns **AcDbGraph::kNullId**" (a different class name than the
  `AcDbEvalGraph::kNullNodeId` used in the same paragraph); `AcDbEvalGraph::getEdgeInfo` parameter docs say
  "**orginating** node"; `AcDbEvalGraph` class description says "reprsent". All kept verbatim.
- **Related classes referenced but not in scope**: `AcDbEvalNodeId` (the node-ID type), `AcDbEvalNodeIdArray`,
  `AcDbEvalEdgeInfo`, `AcDbEvalEdgeInfoArray`, `AcDbEvalContextIterator`, `AcDbEvalConnectable` (a documented
  subclass of `AcDbEvalExpr`). Their own API pages were not part of this collection; they appear here only
  as referenced in signatures/descriptions.
- **Evaluation flow (as documented)**: `AcDbEvalGraph::activate()` marks starting nodes (empty list deactivates
  all; cyclic activation returns `Acad::eGraphCyclesFound`); `AcDbEvalGraph::evaluate()` traverses the DAG
  (topologically sorted subgraph reachable from active nodes) invoking `AcDbEvalExpr::evaluate(ctxt)` on
  visited nodes, with `graphEvalStart/graphEvalEnd/graphEvalAbort` callbacks; `AcDbEvalExpr::value()` returns
  the node's `AcDbEvalVariant` result; `AcDbEvalContext` is a key→void* container (via
  `AcDbEvalContextPair`) passed through evaluation; `AcDbEvalIdMap` maps old→new node IDs after
  `addGraph()` remapping (used by `AcDbEvalExpr::remappedNodeIds()`).
