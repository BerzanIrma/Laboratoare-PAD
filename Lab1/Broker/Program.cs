const int brokerPort = 6000;

var topicManager = new TopicManager();

var persistence = new PersistenceManager(topicManager);
persistence.RestoreIfExists();
_ = persistence.StartAsync(); // rulează în fundal, la fiecare 30s

var server = new BrokerServer(topicManager);
Console.WriteLine("Starting Broker...");
await server.StartAsync(brokerPort);