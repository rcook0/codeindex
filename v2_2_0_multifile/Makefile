.PHONY: schema-validate corpus-check regen-meta

schema-validate:
	python3 tools/validate_schemas.py

corpus-check: schema-validate
	python3 tools/corpus_check.py

regen-meta:
	python3 tools/regen_meta.py

.PHONY: dotnet-build dotnet-java-basic dotnet-tricky-comments

dotnet-build:
	@echo "Building .NET solution (requires dotnet on PATH)..."
	cd src && dotnet build CodeIndex.sln -c Release

dotnet-java-basic: dotnet-build
	cd src && dotnet run --project CodeIndex.Cli/CodeIndex.Cli.csproj -- \
	  --profile ../corpus/profiles.java.json \
	  --input ../corpus/inputs/java_basic.java \
	  --file-id java_basic.java \
	  --out ../out.java_basic.json


dotnet-tricky-comments: dotnet-build
	cd src && dotnet run --project CodeIndex.Cli/CodeIndex.Cli.csproj -- \
	  --profile ../corpus/profiles.java.json \
	  --input ../corpus/inputs/tricky_comments.java \
	  --file-id tricky_comments.java \
	  --out ../out.tricky_comments.json

# 2.1.1 .NET targets

.PHONY: dotnet-subset

dotnet-subset:
	@command -v dotnet >/dev/null 2>&1 || { echo "dotnet not found; install .NET 8 SDK"; exit 2; }
	bash tools/run_subset.sh
