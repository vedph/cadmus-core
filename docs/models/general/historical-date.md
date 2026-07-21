# Historical Date

Historical date, representing either a point or an interval with 1 or 2 points defined on the timeline.

>For this model you can refer to this [conceptual overview](https://cadmus.fusi-soft.com/docs/data-architecture).

🔑 `it.vedph.historical-date`

- date (🧱 [HistoricalDate](https://github.com/vedph/cadmus-bricks/blob/master/docs/historical-date.md)):
  - a* (🧱 [Datation](https://github.com/vedph/cadmus-bricks/blob/master/docs/datation.md)):
    - value* (`int`): the numeric value of the point. Its interpretation depends on other points properties: it may represent a year or a century, or a span between two consecutive Gregorian years.
    - isCentury (`boolean`): true if value is a century number; false if it's a Gregorian year.
    - isSpan (`boolean`): true if the value is the first year of a pair of two consecutive years. This is used for calendars which span across two Gregorian years, e.g. 776/5 BC.
    - slide (`int`): a "slide" delta to be added to value. For instance, value=1230 and slide=10 means 1230-1240; this is not a range in the sense of `HistoricalDate` with its A and B points; it's just a relatively undeterminated point, allowed to move between 1230 and 1240. This means that we can still have a range, like A=1230-1240 and B=1290. A slide is represented by the end year/century value prefixed by `:` in its parsable string. So, we can now have strings like `1230:1240--1290` for range A=1230-1240 and B=1290, or even `1230:1240--1290:1295`; all combinations are possible. With negative (BC) values we have e.g. `810:805 BC` implying slide=5.
    - month (`short`): the month number (1-12) or 0.
    - day (`short`): the day number (1-31) or 0.
    - isApproximate (`boolean`): true if the point is approximate ("about").
    - isDubious (`boolean`): true if the point is dubious ("perhaphs").
    - hint (`string`): a short textual hint used to better explain or motivate the datation point.
  - b (🧱 [Datation](https://github.com/vedph/cadmus-bricks/blob/master/docs/datation.md))
- references (🧱 [DocReference[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/doc-reference.md)):
  - type (`string` 📚 `doc-reference-types`)
  - tag (`string` 📚 `doc-reference-tags`)
  - citation (`string`)
  - note (`string`)
