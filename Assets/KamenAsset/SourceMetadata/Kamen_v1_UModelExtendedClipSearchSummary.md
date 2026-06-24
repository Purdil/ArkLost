# Kamen v1 UModel Extended Clip Search Summary

- Threshold: 0.05
- PSA files scanned: 11
- PSA sequences scanned: 811
- Embedded FBX clips: 492
- Candidate sequences not embedded in FBX: 319
- Failed adjacent pairs checked: 86
- Endpoint pass rows: 7
- Full bridge rows: 0
- One-endpoint rows: 7
- Same group-hint endpoint rows: 7
- Step 7 FBX append/re-export: skipped because no candidate clip passed both endpoint checks; the one-endpoint candidates do not complete a failed adjacent pair.

## PSA Sequence Counts
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_00\AnimSet\mn_cdkcn_00-2_ani.psa: 51 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_00\AnimSet\mn_cdkcn_00_ani.psa: 204 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_00\AnimSet\mn_cdkcn_00_evt2_ani.psa: 29 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_00\AnimSet\mn_cdkcn_02_ani.psa: 212 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_04\AnimSet\mn_cdkcn_04_ani.psa: 240 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_04\AnimSet\mn_cdkcn_04_evt2_ani.psa: 23 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_05\AnimSet\mn_cdkcn_05_ani.psa: 3 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_05\AnimSet\mn_cdkcn_05_evt2_ani.psa: 3 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_08\AnimSet\mn_cdkcn_08_ani.psa: 17 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_09\AnimSet\mn_cdkcn_09_ani.psa: 17 sequences
- Assets\KamenAsset\Models\v1\UModel_KamenSearch\MN_CDKCN_16\AnimSet\mn_cdkcn_16_ani.psa: 12 sequences

## Full Bridge Matches
- None

## First Endpoint Matches
- mn_cdkcn_00_ani__att_status1_4_11_old FAIL/PASS for mn_cdkcn_00-2_ani__att_status1_4_06 -> mn_cdkcn_00-2_ani__att_status1_4_07 (status|mn_cdkcn_00-2_status1_4)
- mn_cdkcn_00_ani__att_status1_4_11_old FAIL/PASS for mn_cdkcn_00-2_ani__att_status1_4_10 -> mn_cdkcn_00-2_ani__att_status1_4_11 (status|mn_cdkcn_00-2_status1_4)
- mn_cdkcn_00_ani__att_status1_5_10_old FAIL/PASS for mn_cdkcn_00-2_ani__att_status1_5_06 -> mn_cdkcn_00-2_ani__att_status1_5_07 (status|mn_cdkcn_00-2_status1_5)
- mn_cdkcn_00_ani__att_status1_5_09_old FAIL/PASS for mn_cdkcn_00-2_ani__att_status1_5_08 -> mn_cdkcn_00-2_ani__att_status1_5_09 (status|mn_cdkcn_00-2_status1_5)
- mn_cdkcn_00_ani__att_status1_4_11_old FAIL/PASS for mn_cdkcn_00_ani__att_status1_4_06 -> mn_cdkcn_00_ani__att_status1_4_07 (status|mn_cdkcn_00_status1_4)
- mn_cdkcn_00_ani__att_status1_5_10_old FAIL/PASS for mn_cdkcn_00_ani__att_status1_5_06 -> mn_cdkcn_00_ani__att_status1_5_07 (status|mn_cdkcn_00_status1_5)
- mn_cdkcn_00_ani__att_status1_5_09_old FAIL/PASS for mn_cdkcn_00_ani__att_status1_5_08 -> mn_cdkcn_00_ani__att_status1_5_09 (status|mn_cdkcn_00_status1_5)

Detailed endpoint passes: `Assets\KamenAsset\SourceMetadata\Kamen_v1_UModelExtendedClipSearchReport.tsv`
Nearest candidates per failed pair: `Assets\KamenAsset\SourceMetadata\Kamen_v1_UModelNearestCandidates.tsv`
