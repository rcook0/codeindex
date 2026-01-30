.PHONY: schema-validate corpus-check regen-meta

schema-validate:
	python3 tools/validate_schemas.py

corpus-check: schema-validate
	python3 tools/corpus_check.py

regen-meta:
	python3 tools/regen_meta.py
