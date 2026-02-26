NOTE: NDJSON and JSONL are identical formats.

# Benchmark

| Model   | Encoding  | Separator  | Format | Latency (s) | Input tokens | Output tokens | Reasoning tokens | Validation |
| ------- | --------- | ---------- | ------ | ----------: | -----------: | ------------: | ---------------: | ---------- |
| gpt-5.2 | Verbose   | Colon      | csv    |         240 |         2967 |         15713 |            13898 | OK         |
| gpt-5.2 | Verbose   | Colon      | json   |         301 |         7009 |         17741 |            15909 | OK         |
| gpt-5.2 | Verbose   | Colon      | ndjson |         235 |         4652 |         14711 |            12896 | OK         |
| gpt-5.2 | Canonical | Colon      | csv    |         365 |         3362 |         22920 |            21086 | OK         |
| gpt-5.2 | Canonical | Colon      | json   |         329 |         7404 |         21030 |            19187 | OK         |
| gpt-5.2 | Canonical | Colon      | ndjson |         187 |         5047 |         13033 |            11206 | OK         |
| gpt-5.2 | Canonical | Underscore | csv    |         366 |         3362 |         22720 |            20896 | OK         |
| gpt-5.2 | Canonical | Underscore | json   |         253 |         7404 |         17016 |            15193 | OK         |
| gpt-5.2 | Canonical | Underscore | ndjson |         246 |         5047 |         15670 |            13818 | OK         |

## 1) The clear winner for speed

**Fastest overall:** **Canonical + Colon + NDJSON**

* Latency: **187,481 ms** (best)
* Total tokens: **18,080**
* Throughput: **96.44 tokens/sec** (best)
* Output throughput: **69.52 output tokens/sec** (best)

## 2) Format matters a lot

* **NDJSON**: fastest average latency (~223s)
* **JSON**: middle (~295s)
* **CSV**: slowest (~324s)

## 3) Canonical vs Verbose is not a free lunch

Comparing **Verbose (Colon)** vs **Canonical (Colon)**, same formats:

### NDJSON (Canonical helps)

* Verbose+Colon NDJSON: **235,154 ms**
* Canonical+Colon NDJSON: **187,481 ms**
* That’s about **20% faster**, with **smaller output** (13,033 vs 14,711) and **less reasoning** (11,206 vs 12,896).

### CSV and JSON (Canonical hurts here)

* CSV: Canonical is **way slower** and outputs **way more tokens** than Verbose.
* JSON: Canonical is slower than Verbose too, and outputs more.

So “Canonical Encoding” is not automatically “better.” It depends on the format and how it changes the model’s behavior and verbosity.

## 4) Separator tweak (Colon vs Underscore) has weird effects

Only measured under **Canonical**:

### JSON: Underscore looks great

* Canonical+Colon JSON: **329,395 ms**
* Canonical+Underscore JSON: **253,832 ms** (**~23% faster**)
* Also fewer output tokens (17,016 vs 21,030)

### NDJSON: Underscore looks worse

* Canonical+Colon NDJSON: **187,481 ms**
* Canonical+Underscore NDJSON: **246,321 ms** (**~31% slower**)
* Also *more* output tokens (15,670 vs 13,033)

So underscore is not “better tokenization” or “better parsing” universally. It changes the shape of the model’s output in format-specific ways.

## 5)  What I would actually conclude (engineering mindset, minimal cope)

**Canonical + Colon + NDJSON** is the fastest and cheapest option
