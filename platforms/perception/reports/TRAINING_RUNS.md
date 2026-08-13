# UniClaw Perception 训练历史（人读视图）

> TRAINING METRICS HAVE ZERO RELEASE AUTHORITY. 本表不按 mAP 排序，不评选『最佳模型』。

DerivedFrom: deploy:101f5ddccd2db3d179de5ed00205f45887442a3e74f443fcdda9f0beb88a71b8|cand:c26b55fd765d70c1787852759cc0ea2c685a6e984676e92c7754bb22401d0837|trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa|run:a90543cfbb748a05a988298a63e01be82848e55dfe409b20d358ee1130cb724c

## Run `trun:4e0038c63409df103a297d1910bad457bb73809867c0dfca61a662633e3b289b`

- Outcome: `RUNNING` — `in-progress`
- Model: `—` / `无 ModelArtifact`
- Dataset: `dataset:c7abafd051d2fb04b6725c800340c03609e6c0ce7f900e30cf70f3dbbc140894`
- TrainingConfig: `tcfg:3b8c746cd4a5b30a6a893f2d31b812db566cee780feb3fe4d091e98b51c9f8be`
- Checkpoint / ModelArtifact:  / 无
- Candidate: `无`
- Evaluation: `无关联 EvaluationRun`
- Purpose: Operational attempt; noncanonical.
- Human note: Operational attempt; noncanonical.

## Run `trun:5cbabd090b58ff973c25d5575d015fd830bedece2fccb691bc297fb35ea7d4b7`

- Outcome: `FAILED` — `failed: RuntimeError: Dataset '/Users/fran/Documents/Code/spacex/uni-agent/platforms/perception/training/artifacts/mini-data/data.yaml' error ❌ Dataset '/Users/fran/Documents/Code/spacex/uni-agent/platforms/perception/training/artifacts/mini-data/data.yaml' images not found, missing path '/Users/fran/Documents/Code/spacex/uni-agent/platforms/perception/training/artifacts/mini-data/images/val'
Note dataset download directory is '/Users/fran/Documents/Code/spacex/datasets'. You can update this in '/Users/fran/Library/Application Support/Ultralytics/settings.json'`
- Model: `—` / `无 ModelArtifact`
- Dataset: `dataset:c7abafd051d2fb04b6725c800340c03609e6c0ce7f900e30cf70f3dbbc140894`
- TrainingConfig: `tcfg:3b8c746cd4a5b30a6a893f2d31b812db566cee780feb3fe4d091e98b51c9f8be`
- Checkpoint / ModelArtifact:  / 无
- Candidate: `无`
- Evaluation: `无关联 EvaluationRun`
- Purpose: FAILED run preserved; no ModelArtifact.
- Human note: FAILED run preserved; no ModelArtifact.

## Run `trun:6f41b678173f93ea41a587f99cd9d12be5884638d12724bfb18ce6123b2b94aa`

- Outcome: `COMPLETED` — `completed`
- Model: `mini_synthetic_box` / `0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8`
- Dataset: `dataset:c7abafd051d2fb04b6725c800340c03609e6c0ce7f900e30cf70f3dbbc140894`
- TrainingConfig: `tcfg:3b8c746cd4a5b30a6a893f2d31b812db566cee780feb3fe4d091e98b51c9f8be`
- Checkpoint / ModelArtifact: `best`→`sha256:0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8`
- Candidate: `cand:c26b55fd765d70c1787852759cc0ea2c685a6e984676e92c7754bb22401d0837`
- Evaluation: `run:a90543cfbb748a05a988298a63e01be82848e55dfe409b20d358ee1130cb724c`
- Purpose: Process closure mini run; not model-quality evidence.
- Human note: Process closure mini run; not model-quality evidence.
