using System.Security.Cryptography;
using System.Text;
using QimiaoDaily.V4.Core;

namespace QimiaoDaily.V4.Publishing;

// This class has deliberately no SDK credentials or HTTP client.  It is the
// production-safe business boundary: it accepts the already locked revision
// and emits an immutable send plan for the official Node SDK runtime.
public sealed class QQOfficialPublisher
{
    public QqPublishPlan CreatePlan(
        ReportRevision revision,
        ReportManifest manifest,
        QqPublishTarget target,
        int maxTextCharacters = 1800)
    {
        if (revision.Revision != manifest.LockedRevision)
            throw new InvalidDataException("QQ publisher requires the manifest's locked revision.");
        if (revision.State is not (ReportState.LockedManual or ReportState.LockedAuto))
            throw new InvalidDataException("QQ publisher requires a locked revision.");
        if (!target.IsValid) throw new InvalidDataException("QQ publish target is invalid.");
        if (maxTextCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maxTextCharacters));

        var hash = Hash(revision.Content);
        if (!string.Equals(hash, revision.ReportHash, StringComparison.Ordinal) ||
            !string.Equals(hash, manifest.ReportHash, StringComparison.Ordinal))
            throw new InvalidDataException("LOCKED_REVISION_HASH_MISMATCH: QQ send blocked.");
        if (!string.Equals(revision.ValidationState, "VALID", StringComparison.Ordinal))
            throw new InvalidDataException("QQ publisher requires a VALID revision.");

        foreach (var artwork in revision.SelectedArtwork)
        {
            if (!artwork.SelectedForReport || !artwork.ReviewStatus.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("QQ publish payload contains unconfirmed artwork.");
        }

        var chunks = Chunk(revision.Content, maxTextCharacters)
            .Select((text, index) => new QqPublishTextChunk(index + 1, text, Hash(text)))
            .ToArray();
        return new QqPublishPlan(revision.Date, revision.Revision, hash, target, chunks, revision.SelectedArtwork, revision.SelectedArtwork.Count > 0);
    }

    // Paragraph first, then item boundaries. An item that does not fit is a
    // hard validation error; publishers must never slice a name or time range.
    private static IReadOnlyList<string> Chunk(string content, int maxCharacters)
    {
        var sections = content.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var current = string.Empty;
        void Flush()
        {
            if (current.Length > 0) result.Add(current);
            current = string.Empty;
        }
        foreach (var section in sections)
        {
            if (section.Length > maxCharacters)
            {
                Flush();
                foreach (var item in section.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (item.Length > maxCharacters)
                        throw new InvalidDataException($"QQ_SECTION_ITEM_TOO_LONG: {item[..Math.Min(48, item.Length)]}");
                    var candidate = current.Length == 0 ? item : current + "\n" + item;
                    if (candidate.Length > maxCharacters)
                    {
                        Flush();
                        current = item;
                    }
                    else current = candidate;
                }
                continue;
            }
            var candidateSection = current.Length == 0 ? section : current + "\n\n" + section;
            if (candidateSection.Length > maxCharacters)
            {
                Flush();
                current = section;
            }
            else current = candidateSection;
        }
        Flush();
        return result;
    }

    private static string Hash(string text) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

public sealed record QqPublishTarget(string TargetType, string TargetId)
{
    public bool IsValid => TargetType is "CHANNEL" or "FORUM" && !string.IsNullOrWhiteSpace(TargetId);
}

public sealed record QqPublishTextChunk(int Sequence, string Text, string Hash);

public sealed record QqPublishPlan(
    DateOnly Date,
    int Revision,
    string ReportHash,
    QqPublishTarget Target,
    IReadOnlyList<QqPublishTextChunk> TextChunks,
    IReadOnlyList<ArtworkRecord> SelectedArtwork,
    bool MediaRequired);
