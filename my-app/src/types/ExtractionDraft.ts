export type ExtractionDraft = {
    name: string;
    expiry: string;          // "YYYY-MM-DD"
    confidence: number;      // 0..1
    notes?: string;
};
