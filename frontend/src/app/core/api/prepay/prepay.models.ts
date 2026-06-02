/** 某信眾今年(含)以前最新報名的預繳資訊（選信眾時自動帶入）。全 null = 查無/無預繳。 */
export interface BelieverLatestPrepay {
  prepayYear: number | null;
  prepayCeremonyCategoryId: string | null;
  prepayCeremonyCategoryTitle: string | null;
}

export interface PrepayLoadRequest {
  sourceYear: number;
  sourceCeremonyId: string;
  targetYear: number;
  targetCeremonyId: string;
  believerGroup: number;
}

export interface PrepayLoadDetails {
  fixedLoaded: number;
  nonFixedLoaded: number;
  carriedForwardPrepay: number;
  filledGaps: number[];
}

export interface PrepayLoadResponse {
  loaded: number;
  skipped: number;
  details: PrepayLoadDetails;
}
