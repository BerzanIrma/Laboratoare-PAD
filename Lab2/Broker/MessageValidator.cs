using System.Text.RegularExpressions;

namespace Broker;

// Verifica datele care vin de la clienti.
// Returneaza mesajul de eroare sau null daca totul e ok.
public static class MessageValidator
{
    public const int MaxTopicLength = 50;
    public const int MaxPublisherLength = 50;
    public const int MaxContentLength = 1000;

    // topicul poate avea doar litere, cifre, '-', '_' si '.', fara spatii
    private static readonly Regex TopicRegex = new(@"^[A-Za-z0-9_.\-]+$");

    public static string? ValidateTopic(string? topic)
    {
        topic = topic?.Trim();

        if (string.IsNullOrEmpty(topic))
            return "Topicul este obligatoriu.";
        if (topic.Length > MaxTopicLength)
            return $"Topicul poate avea maxim {MaxTopicLength} caractere.";
        if (!TopicRegex.IsMatch(topic))
            return "Topicul poate contine doar litere, cifre, '-', '_' si '.'.";

        return null;
    }

    public static string? ValidateMessage(MessageRequest? request)
    {
        if (request == null)
            return "Cererea este goala.";

        if (string.IsNullOrWhiteSpace(request.PublisherId))
            return "Publisher ID este obligatoriu.";
        if (request.PublisherId.Trim().Length > MaxPublisherLength)
            return $"Publisher ID poate avea maxim {MaxPublisherLength} caractere.";

        var topicError = ValidateTopic(request.Topic);
        if (topicError != null)
            return topicError;

        if (string.IsNullOrWhiteSpace(request.Content))
            return "Continutul mesajului este obligatoriu.";
        if (request.Content.Length > MaxContentLength)
            return $"Mesajul poate avea maxim {MaxContentLength} caractere.";

        return null;
    }
}