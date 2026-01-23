namespace AsbExplorer.Models;

public record CoreColumnDefinition(
    string Name,           // "SequenceNumber"
    string Header,         // "#"
    string SqlName,        // "sequence_number"
    string SqlType,        // "INTEGER"
    bool DefaultVisible,
    int MinWidth,
    int MaxWidth
);
