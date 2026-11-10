// Initializeaza replica set-urile rsA si rsB.
// Il ruleaza o singura data serviciul mongo-init din docker-compose.

function initReplicaSet(rsName, prefix) {
    const admin = connect(prefix + "1:27017/admin");

    const config = {
        _id: rsName,
        members: [
            { _id: 0, host: prefix + "1:27017", priority: 2 },  // nodul 1 preferat ca primary
            { _id: 1, host: prefix + "2:27017", priority: 1 },
            { _id: 2, host: prefix + "3:27017", priority: 1 }
        ]
    };

    try {
        admin.adminCommand({ replSetGetStatus: 1 });
        print(rsName + " era deja initializat");
    } catch (e) {
        admin.adminCommand({ replSetInitiate: config });
        print(rsName + " initializat");
    }
}

initReplicaSet("rsA", "mongo-a");
initReplicaSet("rsB", "mongo-b");