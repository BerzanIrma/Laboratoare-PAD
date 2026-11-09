
const hello = db.adminCommand({ hello: 1 });
const currentNode = hello.me;

let config;

if (currentNode.startsWith("mongo-a1")) {
    config = {
        _id: "rsA",
        members: [
            { _id: 0, host: "mongo-a1:27017" },
            { _id: 1, host: "mongo-a2:27017" },
            { _id: 2, host: "mongo-a3:27017" }
        ]
    };
} else if (currentNode.startsWith("mongo-b1")) {
    config = {
        _id: "rsB",
        members: [
            { _id: 0, host: "mongo-b1:27017" },
            { _id: 1, host: "mongo-b2:27017" },
            { _id: 2, host: "mongo-b3:27017" }
        ]
    };
} else {
    throw new Error("Run this script on mongo-a1 or mongo-b1");
}

try {
    rs.status();
    print("Replica set already initialized: " + config._id);
} catch (error) {
    rs.initiate(config);
    print("Replica set initialized: " + config._id);
}
