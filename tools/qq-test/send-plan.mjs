// Pure, transport-independent recovery planning.  Keeping this outside the
// QQ SDK makes the mid-send failure rule testable without creating a post.
export function pendingChunks(chunks, delivered = []) {
  const deliveredBySequence = new Map(delivered.map(item => [item.sequence, item]));
  return chunks.filter(chunk => {
    const prior = deliveredBySequence.get(chunk.sequence);
    if (!prior) return true;
    if (prior.hash !== chunk.hash) {
      throw new Error(`QQ_RESUME_HASH_MISMATCH: chunk ${chunk.sequence}`);
    }
    return false;
  });
}

// `send` is intentionally supplied by the caller.  It may be the real QQ
// transport or a deterministic fake used by tests.  A failure returns an
// explicit partial result; it never retries already delivered chunks.
export async function deliverPendingChunks({ chunks, delivered = [], send }) {
  const allMessages = [...delivered];
  for (const chunk of pendingChunks(chunks, delivered)) {
    try {
      const message = await send(chunk);
      allMessages.push({ ...message, sequence: chunk.sequence, hash: chunk.hash });
    } catch (error) {
      return {
        status: "PARTIAL_FAILURE",
        messages: allMessages,
        failureSequence: chunk.sequence,
        error: error instanceof Error ? error.message : String(error)
      };
    }
  }
  return { status: "COMPLETE", messages: allMessages, failureSequence: null, error: null };
}
