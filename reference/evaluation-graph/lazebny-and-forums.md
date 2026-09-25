# AutoCAD Dynamic Block Evaluation Graph (AcDbEvalGraph / AcDbEvalExpr) — Research Findings

Compiled: 2025-09-25. Sources: A. Lazebny (Supermax / N.N. Polischuk's site) "Mysteries of Autodesk's Caves"
parts 7–12 (English + Russian originals), forum.dwg.ru thread 24597 (all 717 posts), adn-cis.org forum topic 1069,
adn-cis.org "poisk-sosednix-komnat.html", forum.abok.ru topic 14612 (blocked).

## Encoding notes (important)

- The Russian Lazebny pages on BOTH mirrors (poleshchuk.spb.ru and d107535.00067.h001.peterlink.ru) are
  **KOI8-R encoded** (`<meta charset=koi8r>`), NOT cp1251 as the task brief assumed. Fetching them with a
  cp1251/utf-8 assumption yields "???" mojibake (what the task's warning about part 6's double-encoding
  describes). **Decoding the raw bytes as koi8-r recovers the full, clean Russian text** — no iconv
  round-trips needed. All six Russian parts (7–12) were recovered this way (raw HTML saved in
  `.tmp_research/ru_part07..12.html`, extracted text in `.tmp_research/ru_extracted.txt`).
- The English pages (tainypod07e.htm … tainypod12e.htm) are plain ASCII and fetch cleanly.
- forum.dwg.ru is cp1251; decoded with Python `cp1251`. forum.abok.ru is behind a DDoS-Guard JS challenge
  (HTTP 403 for web_fetch, mcp tools, and curl alike).

===============================================================================

# 1. Lazebny, "Mysteries of Autodesk's Caves" — Part 7 "Secrets of ACAD_EVALUATION_GRAPH room"

URLs:
- English: http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod07e.htm (HTTP 200, clean)
- Russian: http://poleshchuk.spb.ru/cad/2009/tainypod07.htm (HTTP 200, koi8-r, recovered clean)

## English verbatim (complete)

> **A.Lazebny. Mysteries of Autodesk's Caves**
>
> **Part 7. Secrets of ACAD_EVALUATION_GRAPH room**
>
> Many users tried many times to swap parameter names in the block properties list but with no success. In what
> sequence these parameters were created - in that sequence they were displayed. My first task was to swap
> parameters.
>
> As I have already written by swapping 95th pairs I razed all. Then I swapped two whole records but they
> returned to their places. Then I swapped only record numbers - numbers returned to theirs places and swapped
> records. Aha, I think, it is a step forward. Looked at block properties list - no visible changes. How is that?
> I changed dictionary in fact, hence somewhere something should vary! I got angry and started pestering block. I
> began with the swapped property. I opened the door leading to that room and dully made Entmod to all the room
> (entmod in AutoLISP means "modify entity"). AutoCAD swore - he said that dotted pairs like 1071 and 1010 were
> unknown. I removed them and made Entmod again. I see - hurrah! Block properties have swapped places!
>
> Now I'll do a digression. The point is that there is such an incantation (it is rather intricate so I will miss
> it) that gets all the properties list for the dynamic block - but after swapping block properties the list
> remained unchanged! Then I shaked the whole block having made Entmod to it. And after that everything changed
> everywhere as it should be.
>
> Now, after long time I think that I would probably shake the whole block at once? I don't know. Let another
> digger test this.
>
> My second sick callus was that block editor allowed only one Visibility Set. If you have for examle 10 objects
> that you want to see in all different visibility states then you must describe 1024 visibilities. And if one
> could insert many Visibility Sets then having taken 10 sets you would need to describe object visibilty only in
> one of them, that is 10 times instead of 1024. And as one Visibility Set can describe visibility for a group of
> elements the total number of combinations is beyond all mind calculations.
>
> I tried to create a new Visibility Set parameter and created it as a copy of an existing one. Inserted. My block
> acquired an amazing quality. If I come in there is no Visibility Set. If I come out it exists in model and in
> block properties. I come in again - none. I set a new one - it is taken. I come out - one more appeared. I come
> in - again desappeared. And so on ad infinitum. Thus I conclude that I may have many such parameters.
>
> After some time I removed my "clumsy" Visibility Set and - oh, wonder! Block editor saw the first in order
> Visibility Set. Moreover editor began to work with it as it should. And since I was already able to swap places
> of block property parameters it turned out that moving to very top any of added Visibility Sets alows to edit it
> with block editor.
>
> I think that I made not a "clumsy" Visibility Set but AutoCAD correctly inserts only its own-made parameters and
> my dummies simply confuse AutoCAD. But it gave some harvest. In such a form my incantations work. Firstly I do a
> dummy then I put it to AutoCAD whose head gets confused and it allows to add one more parameter, and when I
> remove my dummy, AutoCAD recovers and allows to add parameter and when I remove my dummy AutoCAD comes to its
> senses, but it cannot undo and is to handle what it made itself.

## Key findings (part 7)

1. **Swapping 95 pairs destroys the graph** ("I razed all").
2. **Swapping two whole records (including record numbers) is reverted by AutoCAD** after entmod — record order
   in the dictionary is strictly by record number.
3. **Swapping only the record numbers (91) works**: after entmod the record order is restored by number, but the
   *content* of the records has swapped → properties swap places in the block Properties palette.
4. **entmod on the ACAD_EVALUATION_GRAPH dictionary rejects codes 1071 and 1010** ("AutoCAD swore ... dotted pairs
   like 1071 and 1010 were unknown. I removed them and made Entmod again.") — i.e. 1071/1010 must be stripped from
   the *parameter* objects before entmod; they are regenerated.
5. **The cached `vla-getdynamicblockproperties` list is not refreshed by entmod on the graph dictionary** — you must
   also entmod the block itself ("shaked the whole block") for all views to update.
6. **Multiple Visibility Sets**: block editor only honors the **first** Visibility Set in the graph's record order;
   moving any added Visibility Set record to the top of the graph makes the editor edit it. A "dummy" Visibility
   Set inserted via entmakex makes AutoCAD "confused" so that the next real (command-inserted) Visibility Set is
   accepted → a block can accumulate many Visibility Set parameters.

## Russian original (key excerpts, clean koi8-r decode)

> **Часть 7. Секреты комнаты ACAD_EVALUATION_GRAPH**
>
> Как я уже писал, при попытке переставить 95 пары, я разваливал все до основания. Тогда я взял и переставил
> местами две записи целиком - а они на старое место вернулись. Тогда я переставил только номера записей - при
> рассмотрении того, что получилось, оказалось, что номера вернулись на свои места, а вот начинка записей
> поменялась местами. ... Я открыл дверь в ту комнату и тупо сделал всей комнате Entmod ... Автокад ругнулся, что,
> дескать, такие точечные пары, как 1071 и 1010, ему не знакомы. Я их убрал и опять сделал Entmod. Гляжу - ура!
> Свойства в блоке поменялись местами.
>
> ...Тогда я взял и тряхнул весь блок. Сделав Entmod ему в целом. И вот только тогда все везде поменялось, как
> положено.
>
> ...я удалил свой "корявый" Visibility Set и - о чудо! Редактор блока увидел первый по списку Visibility Set. ...
> оказалось, что перемещение в самый верх любого из установленных Visibility Set-ов дает возможность их
> редактировать редактором блока.

===============================================================================

# 2. Lazebny — Part 8 "Where block elements are buried"

URLs:
- English: http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod08e.htm (HTTP 200, clean; full text saved in
  `.tmp_research/en_part08.txt`)
- Russian: http://poleshchuk.spb.ru/cad/2009/tainypod08.htm (HTTP 200, koi8-r, recovered clean)

The part walks from the VLA application object down to the block's extension dictionary and the
ACAD_EVALUATION_GRAPH. Navigation path (verbatim from the English text):

> Enter into the main entrance
> └> Next - to ActiveDocument room
> └> Next - to Blocks collection
> └> Find block with required name
> └> Get from DXF description dotted pair 360 - (0 . "DICTIONARY")
> └> Hence - pair 360 - (0 . "ACAD_EVALUATION_GRAPH")

## AutoLISP navigation code (verbatim)

```lisp
(vlax-get-acad-object)
(vlax-vla-object->ename (vlax-get-acad-object))   ; -> nil (application has no ename)
(vlax-dump-object (vlax-get-acad-object))
(vlax-dump-object (vlax-get-acad-object) T)
(vla-get-ActiveDocument (vlax-get-acad-object))

(setq sss nil)
(vlax-for x (vla-get-Blocks (vla-get-ActiveDocument (vlax-get-acad-object)))
  (setq sss (append sss (list x))))
; -> (#<VLA-OBJECT IAcadModelSpace ...> #<VLA-OBJECT IAcadPaperSpace ...>
;     #<VLA-OBJECT IAcadBlock ...> #<VLA-OBJECT IAcadBlock ...>)

; block record of the user block "линия" (line):
(entget (vlax-vla-object->ename (car (reverse sss))))
```

## DXF dump 1 — BLOCK_RECORD of the user block (English part 8; 310 binary blob abridged, identical in Russian)

```
((-1 . <Entity name: 7ef97080>)
(0 . "BLOCK_RECORD")
(330 . <Entity name: 7ef95c08>)
(5 . "110")
(100 . "AcDbSymbolTableRecord")
(100 . "AcDbBlockTableRecord")
(2 . "линия")
(360 . <Entity name: 7ef97088>)
(340 . <Entity name: 0>)
(310 . "2800000020000000200000000100080000000000000400000000000000000000000000000000000000000000000080000080000000808000800000008000800080800000C0C0C000C0DCC000F0CAA6000020400000206000002080000020A0000020C0000020E00000400000004020000040400000406000004080000040A0" ...)  ; ~20 x 310 pairs, abridged
(102 . "{BLKREFS") (331 . <Entity name: 7ef970a0>) (102 . "}")
(70 . 4)
(280 . 1)
(281 . 0))
```

## DXF dump 2 — the block's extension DICTIONARY (verbatim)

```
((-1 . <Entity name: 7efdd0d0>)
(0 . "DICTIONARY")
(330 . <Entity name: 7efdd080>)
(5 . "11A")
(100 . "AcDbDictionary")
(280 . 1)
(281 . 1)
(3 . "ACAD_ENHANCEDBLOCK")
(360 . <Entity name: 7efdd0d8>)
(3 . "AcDbDynamicBlockRoundTripPurgePreventer")
(360 . <Entity name: 7efdd138>))
```

## DXF dump 3 — ACAD_EVALUATION_GRAPH of a block with one line + one Visibility Set (verbatim, English part 8)

```
((-1 . <Entity name: 7efdd0d8>)
(0 . "ACAD_EVALUATION_GRAPH")
(5 . "11B")
(102 . "{ACAD_REACTORS") (330 . <Entity name: 7efdd0d0>) (102 . "}")
(330 . <Entity name: 7efdd0d0>)
(100 . "AcDbEvalGraph")
(96 . 4)
(97 . 4)
(91 . 0) (93 . 32) (95 . 1) (360 . <Entity name: 7efdd0e8>) (92 . 0) (92 . 0) (92 . 1) (92 . 2)
(91 . 1) (93 . 32) (95 . 2) (360 . <Entity name: 7efdd0f0>) (92 . -1) (92 . -1) (92 . 0) (92 . 0)
(91 . 2) (93 . 32) (95 . 3) (360 . <Entity name: 7efdd0f8>) (92 . 1) (92 . 1) (92 . -1) (92 . -1)
(91 . 3) (93 . 32) (95 . 4) (360 . <Entity name: 7efdd100>) (92 . 2) (92 . 2) (92 . -1) (92 . -1)
(92 . 0) (93 . 0) (94 . 1) (91 . 1) (91 . 0) (92 . -1) (92 . -1) (92 . -1) (92 . -1) (92 . -1)
(92 . 1) (93 . 0) (94 . 1) (91 . 0) (91 . 2) (92 . -1) (92 . -1) (92 . -1) (92 . 2) (92 . -1)
(92 . 2) (93 . 0) (94 . 1) (91 . 0) (91 . 3) (92 . -1) (92 . -1) (92 . 1) (92 . -1) (92 . -1))
```

Record layout: 4 main records (93 . 32): [0]=BLOCKVISIBILITYPARAMETER, [1]=BLOCKVISIBILITYGRIP,
[2]=BLOCKGRIPLOCATIONCOMPONENT "UpdatedX", [3]=BLOCKGRIPLOCATIONCOMPONENT "UpdatedY";
followed by 3 extended (93 . 0) records chained via 92.

## DXF dump 4 — BLOCKVISIBILITYPARAMETER (verbatim, English part 8)

```
((-1 . <Entity name: 7efdd0e8>)
(0 . "BLOCKVISIBILITYPARAMETER")
(330 . <Entity name: 7efdd0d8>)
(5 . "11D")
(100 . "AcDbEvalExpr")
(90 . 1)
(98 . 27)
(99 . 1)
(100 . "AcDbBlockElement")
(300 . "Visibility State")
(98 . 27)
(99 . 1)
(1071 . 16)
(100 . "AcDbBlockParameter")
(280 . 1)
(281 . 0)
(100 . "AcDbBlock1PtParameter")
(1010 561.266 110.92 0.0)
(93 . 2)
(170 . 0)
(171 . 0)
(100 . "AcDbBlockVisibilityParameter")
(281 . 1)
(301 . "Visibility")
(302 . "")
(91 . 0)
(93 . 1)
(331 . <Entity name: 7efdd0c0>)
(92 . 1)
(303 . "VisibilityState0")
(94 . 1)
(332 . <Entity name: 7efdd0c0>)
(95 . 0))
```

## Code meanings given in part 8 (verbatim, English)

> Starting from the code -1 and up to code 302 only two codes are interesting: 300 and 301.
>
> **300** is Parameter name from the list Visibility Set object properties. If you open block editor and highlight
> Visibility Set manually then you will find this parameter in the properties list and will be able to modify it.
>
> **301** in the same place is Visibility label. It's what we see as written on the object Visibility Set in block
> editor.
>
> **302** in the same place is Visibility description. It is a string too. Label in block properties list, names for
> the drop-down list of Visibility Set. It is written to the left of the drop-down list.
>
> And running ahead: **303** is visibility state name. Number of 303s is the same as number of these states.
>
> All the other codes to 91 are of no interest for pioneers in digging blocks.
>
> **Code 91** is a current representation (Visibility State): 0 is a first one, 1 is a second one, etc.
>
> **Code 93** (do not mix with code 93 at the beginning of the list) is a number of graphical and textual elements in
> the block including attributes. General number of elements, that's to say.
>
> This code is followed by codes **331** with references to elements themselves. There are no properties in the
> list. What for the list is I will tell later.
>
> **Code 92** is a number of representations in the current Visibility Set.
>
> And now the most interesting.
>
> **Code 303** is a representation name (the very first representation in the list is numbered with 0).
>
> **Code 94** is a number of elements in the representation.
>
> **Codes 332** are pointers to the elements visible in this representation.
>
> **Code 95** is a number of properties visible in this representation (Visibility Set property is not counted).
>
> And if besides Visibility Set there are any properties more then further codes **333** with pointers to these
> properties follow.
>
> If representations are numerous then records 303, 94, 332s, 95, 333s are repeated.
>
> If there are no elements in the representation (that is (94 . 0)), then there are no codes 332. If representation
> has no properties (that is (95 . 0)), then there are no codes 333 either.

## Evaluation semantics (verbatim, English part 8)

> The full block elements list as well as its properties list is necessary to switch visibility off while passing
> from one representation to another, and elements list in a record with representation name is needed to switch
> visibility on. **That is AutoCAD while every switching firstly switches off all that is in the record with codes
> 331 and all the properties, and then using record with codes 332 and 95 after the dotted pair with representation
> name switches on all that is shown there.**
>
> I removed single element from the list of all the elements and from the list of elements in the representation
> record (having changed values of pairs 93 and 94 to less numbers), and everything works just so. There is a real
> possibility to manage some block elements from one BLOCKVISIBILITYPARAMETER and some from another, and they will
> not interfere with each other (only if there are no identic elements in common lists).
>
> The fact that block editor is unable to handle several Visibility Sets is not harmful, with the help of a simple
> program all is arranged in the best way.
>
> **If one opens entity with entget function in block editor space then one will find that its code -1 is the same
> as code 331 and 332 in this dictionary. Codes 360 of functions in the ACAD_EVALUATION_GRAPH dictionary are
> identic to codes 333 in BLOCKVISIBILITYPARAMETER dictionary.**
>
> Mapping parameters in block editor space and in ACAD_EVALUATION_GRAPH will be given later on. What you see in the
> topic is not a fine solution.

## Russian original (part 8, key excerpts)

The Russian part 8 carries the same dumps (identical handles 7efdd0d8/7efdd0e8) and the same code explanations:

> 91 код - текущее представление (Visibility State): 0 - первое, 1 - второе и т.д.
>
> 93 код (не путать с 93 кодом в начале списка) - количество графических и текстовых элементов в блоке, включая и
> атрибуты. Общее, так сказать, количество элементов.
>
> За этим кодом следует 331 коды с указателями на сами элементы. Свойств в этом списке нет.
>
> 92 код - количество представлений в данном Visibility Set-e.
>
> 303 код - имя представления (самое первое представление в данном списке имеет N 0).
>
> 94 код - количество элементов в этом представлении.
>
> 332 коды - перечень указателей на те элементы, которые видны в этом представлении.
>
> 95 код - количество свойств, видимых в этом представлении (свойство Visibility Set не считается).
>
> ...Автокад при каждом переключении видимости сначала все, что в записи с 331 кодами, и все свойства блока
> выключает, а затем по записи с 332 кодами и 95-ми, следующими после точечной пары с именем представления, все,
> что там указано, включает.
>
> Если в пространстве редактора блока функцией entget открыть примитив, то его -1 код и есть тот 331 и 332 код в
> этом словаре. 360 коды функций в словаре "ACAD_EVALUATION_GRAPH" и есть 333 коды в словаре
> "BLOCKVISIBILITYPARAMETER".

===============================================================================

# 3. Lazebny — Part 9 "Lookup and his friends"

URLs:
- English: http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod09e.htm (HTTP 200, clean)
- Russian: http://poleshchuk.spb.ru/cad/2009/tainypod09.htm (HTTP 200, koi8-r, recovered clean)

## English verbatim (complete)

> **Part 9. Lookup and his friends**
>
> Now it's time to rell about 95th dotted pair in ACAD_EVALUATION_GRAPH and Lookup parameter where there is a link
> opening doors into never seen dynamism.
>
> First of all what is Lookup? Lookup is such a parameter that controls all the rest parameters. Suppose that you
> need simultaneously to move eyes and to remove nose. Hence it is necessary to make a parameter for moving eyes
> and Visibility parameter for managing nose visibility.
>
> This can be made in three steps. First state: nose yes, eyes look up. Second state: eyes look straight, nose yet
> yes. Third state: eyes came to nose, nose disappeared.
>
> Lookup is a table where in the first column you choose parameter moving the first eye. In the second column there
> is a parameter to move second eye and the third one there is a Visibility parameter switching representations. In
> the column to the right we put parameters naming states: "look up", "look straight", "look to the nose".
>
> If we select block then in the empty zone a cyan triangle appears. If we click it a popup menu with three items
> ("look up", "look straight", "look to the nose") opens. Click at any state and Lookup transforms all the
> parameters to required mode. **And this was till the moment when I decided to pick this Lookup.**
>
> A little digression. The fact is sorcerers from Autodesk blocked possibility to set mre than one activator to
> Lookup parameter. And table is such an activator.
>
> How activator is linked with parameter? It turns out that only by one dotted pair. Let's add Lookup to our block
> and try to see its links. (I killed Visibility Set - it is unneeded).

## DXF dump 5 — ACAD_EVALUATION_GRAPH of a block with one Lookup parameter (verbatim)

```
((-1 . <Entity name: 7efdd248>)
(0 . "ACAD_EVALUATION_GRAPH")
(5 . "149")
(102 . "{ACAD_REACTORS") (330 . <Entity name: 7efdd240>) (102 . "}")
(330 . <Entity name: 7efdd240>)
(100 . "AcDbEvalGraph")
(96 . 9)
(97 . 9)
(91 . 0) (93 . 32) (95 . 5) (360 . <Entity name: 7efdd288>) (92 . 0) (92 . 3) (92 . 1) (92 . 2)
(91 . 1) (93 . 32) (95 . 6) (360 . <Entity name: 7efdd290>) (92 . -1) (92 . -1) (92 . 0) (92 . 0)
(91 . 2) (93 . 32) (95 . 7) (360 . <Entity name: 7efdd298>) (92 . 1) (92 . 1) (92 . -1) (92 . -1)
(91 . 3) (93 . 32) (95 . 8) (360 . <Entity name: 7efdd2a0>) (92 . 2) (92 . 2) (92 . -1) (92 . -1)
(91 . 4) (93 . 32) (95 . 9) (360 . <Entity name: 7efdd2b8>) (92 . -1) (92 . -1) (92 . 3) (92 . 3)
(92 . 0) (93 . 0) (94 . 1) (91 . 1) (91 . 0) (92 . -1) (92 . 3) (92 . -1) (92 . -1) (92 . -1)
(92 . 1) (93 . 0) (94 . 1) (91 . 0) (91 . 2) (92 . -1) (92 . -1) (92 . -1) (92 . 2) (92 . -1)
(92 . 2) (93 . 0) (94 . 1) (91 . 0) (91 . 3) (92 . -1) (92 . -1) (92 . 1) (92 . -1) (92 . -1)
(92 . 3) (93 . 0) (94 . 1) (91 . 4) (91 . 0) (92 . 0) (92 . -1) (92 . -1) (92 . -1) (92 . -1))
```

> <Entity name: 7efdd288> is Lookup parameter.
> <Entity name: 7efdd290> is its grip.
> <Entity name: 7efdd298> is X position.
> <Entity name: 7efdd2a0> is Y position.
> <Entity name: 7efdd2b8> is activator.

Note the 92 chain: record [0] (Lookup parameter) has 92 = (0, 3, 1, 2): main marker 0, "adopted parent" marker 3
(the activator record), child #1 = record 1 (grip), child #2 = record 2 (UpdatedX). Record [3] (UpdatedY) has 92 =
(2, 2, -1, -1): adopted parent = record 2 (UpdatedX) — the UpdatedX/UpdatedY pair is linked through the 92
"adopted parent" field. Record [4] (activator) has 92 = (-1, -1, 3, 3): both children point to record 3.

## DXF dump 6 — BLOCKLOOKUPACTION (the Lookup activator) (verbatim)

```
(entget (cdr (assoc 360 (reverse EVAL_GRAPH)))

((-1 . <Entity name: 7efdd2b8>)
(0 . "BLOCKLOOKUPACTION")
(330 . <Entity name: 7efdd248>)
(5 . "157")
(100 . "AcDbEvalExpr")
(90 . 9)
(98 . 27)
(99 . 1)
(100 . "AcDbBlockElement")
(300 . "Lookup1")
(98 . 27)
(99 . 1)
(1071 . 8)
(100 . "AcDbBlockAction")
(70 . 0)
(71 . 0)
(1010 471.79 -132.523 0.0)
(100 . "AcDbBlockLookupAction")
(92 . 0)
(93 . 1)
(301 . "")
(303 . "")
(94 . 5)
(95 . 1)
(96 . 0)
(282 . 1)
(305 . "Custom")
(281 . 0)
(304 . "lookupString")
(280 . 1))
```

## Key findings (part 9)

> **Pay attention to pair 94 in the activator. It always points to 95th pair value in that parameter record of
> ACAD_EVALUATION_GRAPH to which activator is linked. If you change this pair in the activator to another pair
> pointing to other parameter then activator will work for it.**

> Now I described movement of the first eye by table in one activator, movement of the other eye in another
> activator and nose visibility in the third one. All of them are connected to one parameter and I am not obliged
> to select statue states from the list. I take the eye and pull. Firstly both eyes go to the middle part, after
> that they go down and nose disappears.

- One Lookup parameter can have **multiple activators** (each a separate BLOCKLOOKUPACTION record in the graph);
  Autodesk's UI only allows one, but the format does not.
- The `94` pair inside the AcDbBlockLookupAction section is the link from the activator to the parameter: it
  equals the `95` (creation number) of the target parameter's graph record.
- `1071 . 8` here vs `1071 . 16` for the Visibility parameter — 1071 is a per-record tag (values seen across dumps:
  0, 8, 16).

## Russian original (part 9, key excerpts)

> Обратите внимание на 94 пару в активаторе. Она всегда показывает на значение 95 пары в записи того параметра в
> "ACAD_EVALUATION_GRAPH", к которому привязан активатор. Если в активаторе изменить эту пару на другую,
> показывающую на другой параметр, то активатор будет работать на него.

===============================================================================

# 4. Lazebny — Part 10 "Cottage having neither windows nor doors"

URLs:
- English: http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod10e.htm (HTTP 200, clean)
- Russian: http://poleshchuk.spb.ru/cad/2009/tainypod10.htm (HTTP 200, koi8-r, recovered clean)

## English verbatim (complete)

> **Part 10. Cottage having neither windows nor doors**
>
> Dear Messrs tourists and pretending to be them, let us together move to blockeditor space and solve a very
> important task. Choose some parameter, grip or activator and try to find mapping to this object in the room
> ACAD_EVALUATION_GRAPH. What for? To be able create such incantations that would process some required actions
> with these parameters, grips and activators. Do you remeber what I wrote earlier?
>
> **_... If one opens an entity with the entget function in block editor space then code -1 will be similar to
> codes 331 and 332 in this dictionary. Codes 360 of functions in the ACAD_EVALUATION_GRAPH dictionary are equal to
> codes 333 in the BLOCKVISIBILITYPARAMETER dictionary. ..._**
>
> Ah, I have forgotten! Children, if you pronounce (entget (car (entsel))) in block editor space and hit your
> hand-made production (parameters were not made by you but by castle and do not hit them), then to be opened will
> have the same number that the doors in the BLOCKVISIBILITYPARAMETER room.
>
> And if we pronounce this incantation and hit some parameter, grip or activator then we will get:
>
> ```
> ((-1 . <Entity name: 7ef97118>))
> ```
>
> - unpretentious room where find nothing. We can convert this door to vla-door and look at properties and methods
> but we will reach nothing. We won't answer the question "how to know the door number corresponding to this
> parameter in ACAD_EVALUATION_GRAPH?". Better to say, it is not absolutely so. Parameters and activators have
> names. By names you can find room name in the doors list of ACAD_EVALUATION_GRAPH. Next you can watch links from
> codes 92 and calculate grips and activators, but it very laborious - like to put on trousers over the head.
>
> We will pass to other path. ... We'll gather all the elements including parameters, grips and activators into a
> common heap with the help of incantation (ssget "\_X") and form a list of gathered objects for providing
> exclusion of those elements that after (entget (car (entsel)) reply with a list having more than 1 pair.
>
> And we will also come into the ACAD_EVALUATION_GRAPH room and there make up a list of all the doors to dynamic
> parameters so that in the list there would be no room numbers pointing to position and the first door would be
> the first in the list. ... (setq la-la-la (append (list tru-la-la) la-la-la)) ... or (setq la-la-la (reverse
> la-la-la)). It is very important! **Because you will get such a list that is identical to the list created from
> the gathered heap in block editor space!** What is there on the first place is on the first place here? what is on
> the second is on the second and so on.
>
> **As soon as you move pair 91 with record number in the ACAD_EVALUATION_GRAPH room then at once in block editor
> space a set is being created with modified position. Moved there - relocated here.**
>
> Strictly speaking, it's all.

## Key findings (part 10)

- In block editor space, `entget` on a parameter/grip/activator returns **only `(-1 . <Entity name>)`** — no DXF
  data at all. The only way to map a block-editor object to its graph record is:
  1. gather all block-editor elements with `(ssget "_X")` (excluding elements whose entget has more than 1 pair),
  2. gather all parameter doors in the graph (excluding position/UpdatedX/UpdatedY records), keeping the order,
  3. the two lists are **identical in order** — index i in one list corresponds to index i in the other.
- Reordering the 91 record numbers in the graph reorders the block-editor objects.

## Russian original (part 10, key excerpts)

> А вот если произнести это заклинание и ткнуть в какой-нибудь параметр, или ручку, или активатор, то мы
> получим: `((-1 . <Entity name: 7ef97118>))` - скромное помещение, где нет ничего.
>
> ...соберем все элементы, включая и параметры, и ручки, и активаторы, в общую кучу с помощью заклинания
> (ssget "_X") ... Так же мы зайдем в комнату "ACAD_EVALUATION_GRAPH" и там составим список всех дверей в
> динамические параметры ... Это очень важно! Потому что то, что у вас получится, - список, идентичный тому
> списку, который создан из собранной кучи в пространстве редактора!
>
> Как только передвигаешь 91 пары с номером записи в комнате "ACAD_EVALUATION_GRAPH", так сразу в пространстве
> редактора блоков набор создается с измененным положением. Там передвинул - тут переместилось.

===============================================================================

# 5. Lazebny — Part 11 "How digger Supermax and sorcerer Andrejus decided to join efforts"

URLs:
- English: http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod11e.htm (HTTP 200, clean)
- Russian: http://poleshchuk.spb.ru/cad/2009/tainypod11.htm (HTTP 200, koi8-r, recovered clean)

**Not about the graph format.** The part is a history of building an HTML (modeless) interface for AutoLISP
("webmacro"): the author first asked at dwg.ru, then at the "Grey forum" (forum.script-coding.info) where
**wisgest** and **The gray Cardinal** solved the core problem (link:
http://forum.script-coding.info/viewtopic.php?id=1187), then finished at www.midoma.ru, and together with
**Andrejus** (HTML) and Anatoly (admin of midoma.ru, the financier) produced a working webmacro for controlling
object visibility (link: http://www.midoma.ru/node/upravlieniie-vidimost-iu-ob-iektov).

English verbatim (complete, abridged of the forum-lore middle):

> Some sorcerers use pentagrams for their charms, others prefer big boilers ... Modal interface is a tool when you
> should do nothing apart from working with it ... Modeless interface is a better tool for you can hang it in the
> corner and calmly practise sorcery or fry eggs. ... The most famous and widely used one is HTML. ... So there was
> born an idea to use HTML interface for AutoLISP language. ... I started with searching an appropriate sorcerer at
> the well-known institution DWG.RU but found nobody. After that I screw up my courage and had a look at the place
> that mere mortal people do not attend: Grey forum ... I found two sorcerers: **wisgest** and **The gray Cardinal**.
> They solved my problem without leaving their beer mugs. More exactly, wisgest solved and The gray Cardinal
> improves the solution. [Here it is](http://forum.script-coding.info/viewtopic.php?id=1187). ... Afterwards I
> passed to a more calm place - www.midoma.ru, where wisgest helped me to close my trouble. ... fortune brought me
> together with a sorcerer Andrejus. I needed a promoter and such a man found - Anatoly, admin of modoma.ru. He
> became a customer and a financier for creation of the first specimen of webmacro ... In a couple of months we gave
> birth to this masterpiece ...

===============================================================================

# 6. Lazebny — Part 12 "Appendix: Incantations (AutoLISP functions) of digger Supermax"

URLs:
- English: http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod12e.htm (HTTP 200, clean)
- Russian: http://poleshchuk.spb.ru/cad/2009/tainypod12.htm (HTTP 200, koi8-r, recovered clean)

## English verbatim (complete)

> **Appendix to "Mysteries of Autodesk's Caves"**
>
> **Incantations (AutoLISP functions) of digger Supermax**
>
> You can dowload and unzip visibility-add-eng.lsp file with source texts of the following A.Lazebny's programs:
>
> 1. eval_graf_output - getting ACAD_EVALUATION_GRAPH dictionary from the block editor space
> 2. visibility_add - adding a new Visibility Set parameter
> 3. visibility-up - setting selected Visibility Set as current
> 4. eddedd - switching grips on for all the elements of the current Visibility Set
> 5. tecuch_visibility - retrieving name of the current Visibility Set
> 6. blk-visib-param-auditor - auditing BLOCKVISIBILITYPARAMETER dictionary
> 7. element-sel-current-del - removing selected elements from the current Visibility Set
> 8. element-all-current-del - removing all the elements from the current Visibility Set
> 9. element-sel-current-insert - adding selected elements to current Visibility Set
> 10. visibility_clear - complete cleaning current state from all the elements, dynamic properties and states
> 11. properties_add_all_visibility - setting visibility of all the dynamic properties and grips in all the states
>     of all Visibility Sets
> 12. sootvetstvie - determining accordance of element pointer in the block editor space with
>     ACAD_EVALUATION_GRAPH and BLOCKVISIBILITYPARAMETER dictionaries
>
> Versions for download:
> **29.01.09**. Version 1.0.
> **15.02.09**. Version 1.1 (and move-to-visibilityset function is added).
> **24.07.09**. Version 1.2 (element-sel-current-insert function was changed).
> **24.10.09**. Version 1.3 (eval_graf_output function was changed).
> **11.02.10**. visibility-add-eng-v1.4, move-properties-eng-v1.4 - **support of properties tables in AutoCAD 2010
> dynamic blocks added**.
> **22.10.15**. visibility-add-eng_1_6.zip (update by DBdJ1) can work in AutoCAD 2016.
>
> And now an example of dynamic block with 10 visibility parameters (vp01, vp02, ..., vp10): [superblock.png]
> This DWG file is here. To create such a block from a usual dynamic block with one visibility parameter you should
> run steps:
>
> 1. Load visibility-add-eng.lsp in the block editor window containing your usual dynamic block
> 2. Add a new visibility parameter with (visibility_add)
> 3. Move new visibility parameter up with (visibility-up)
> 4. Add and edit visibility parameter states
> 5. Repeat steps 2-4 for other visibility parameters
> 6. Save your dynamic block
> Enjoy!
>
> **17.02.09**. Addition: function move-properties for moving custom properties up or down in dynamic blocks
> (e.g., in blocks used for commercial products). ... Launch: (move-properties).
>
> **11.02.10**. See above on a new version.
>
> **24.07.09**. One more example of dynamic block with several visibility parameters (by T.Bennett)
> (http://discussion.autodesk.com/forums/thread.jspa?threadID=736387&tstart=0).
>
> **22.10.15**. Good news: thanks to DBdJ1 the program works in AutoCAD 2016!
> (http://forums.autodesk.com/t5/dynamic-blocks/multiple-visibility-lisp-routine/m-p/5872400#M19188)

## Russian original (part 12, key excerpts — version history)

> Версии для загрузки:
> 29.01.09. Версия 1.0.
> 15.02.09. Версия 1.1 (кроме того, добавлена функция move-to-visibilityset).
> 24.07.09. Версия 1.2 (изменена функция element-sel-current-insert).
> 24.10.09. Версия 1.3 (изменена функция eval_graf_output).
> **11.02.10. visibility-add-rus-v1.4, move-properties-rus-v1.4 - добавлена поддержка таблиц свойств в динамических
> блоках AutoCAD 2010.**
> 22.10.15. visibility-add-eng_1_6.zip (изменения DBdJ1) может работать в AutoCAD 2016.

**Version statement:** AutoCAD **2010** introduced **property tables** ("таблицы свойств" / "properties tables") in
dynamic blocks; the Lisp toolkit was adapted on 11.02.10 to handle them, and a 2016-compatible update appeared
22.10.15 (by DBdJ1).

===============================================================================
===============================================================================

# 7. forum.dwg.ru thread 24597 — "Создание дополнительных параметров Visibility Set в динамических блоках"

URL: https://forum.dwg.ru/showthread.php?t=24597
- 717 posts, 36 pages. **All 36 pages downloaded** (raw cp1251 HTML in `.tmp_research/dwg_raw/p01..p36.html`,
  full extracted text in `.tmp_research/dwg_full_thread.txt`). This is the thread behind Lazebny part 6 and the
  source of the code explanations quoted in part 8.

## Post #1 by Supermax (13.09.2008) — thread index (verbatim, key lines)

> В этой теме рассматривается вопрос программного "наращивания" возможностей динамических блоков.
>
> Программистам: Разбор словарей дин.блока; Получение параметра видимости и его разбор; Примеры блоков с
> несколькими параметами видимости (Visibility Set): "неправильные" и "правильные". В посте #212 - пример связи
> Lookup-ов друг с другом. Макрос по перемещению свойств в списке свойств блока меню Properties.
>
> **02.02.2010 Откорректирована для работы в 2010 каде**
>
> Пользователям: Готовые макросы: Все функции по вставке и обработке дополнительных Visibility Set.
> **Обновление 02.02.2010. !!! Откорректирована в связи с появлением нового динамического элемента в 2010 каде**
>
> (properties_add_all_visibility) - установка видимости выбранных динамических свойств и ручек во всех
> представлениях всех Visibility Set-ов. ... (element-all-current-del) ... (element-sel-current-del) ...
> (element-sel-current-insert) ... (Visibility_add) - Добавление нового Visibility Set-a ... (visibility-up) -
> Делает указанный пользователем Visibility Set текущим ... (eddedd) - Включает ручки всем элементам текущего
> Visibility Set-a ... (Visibility_clear) - Очищает указанный Visibility Set от всех элементов, параметров и
> представлений. ... Excel->Lookup; Lookup->Excel; Lookup->Lookup. ...
>
> Исходные тексты к функциям: http://www.private.peterlink.ru/pole...9/tainypod.htm

## Post #6 by Supermax (13.09.2008) — first ACAD_EVALUATION_GRAPH dump (verbatim)

> Вот распечатка "ACAD_EVALUATION_GRAPH" не очень насыщенного свойствами блока.

```
((-1 . <Entity name: 7bf53288>)
(0 . "ACAD_EVALUATION_GRAPH")
(5 . "37EF9")
(102 . "{ACAD_REACTORS") (330 . <Entity name: 7bf53280>) (102 . "}")
(330 . <Entity name: 7bf53280>)
(100 . "AcDbEvalGraph")
(96 . 14)
(97 . 14)
(91 . 0) (93 . 32) (95 . 11) (360 . <Entity name: 7bf53308>) (92 . 0) (92 . 0) (92 . 1) (92 . 2) BLOCKVISIBILITYPARAMETER
(91 . 1) (93 . 32) (95 . 12) (360 . <Entity name: 7bf53310>) (92 . -1) (92 . -1) (92 . 0) (92 . 0) BLOCKVISIBILITYGRIP
(91 . 2) (93 . 32) (95 . 13) (360 . <Entity name: 7bf53318>) (92 . 1) (92 . 1) (92 . -1) (92 . -1) BLOCKGRIPLOCATIONCOMPONENT "UpdatedX"
(91 . 3) (93 . 32) (95 . 14) (360 . <Entity name: 7bf53320>) (92 . 2) (92 . 2) (92 . -1) (92 . -1) BLOCKGRIPLOCATIONCOMPONENT "UpdatedY"
(92 . 0) (93 . 0) (94 . 1) (91 . 1) (91 . 0) (92 . -1) (92 . -1) (92 . -1) (92 . -1) (92 . -1)
(92 . 1) (93 . 0) (94 . 1) (91 . 0) (91 . 2) (92 . -1) (92 . -1) (92 . -1) (92 . 2) (92 . -1)
(92 . 2) (93 . 0) (94 . 1) (91 . 0) (91 . 3) (92 . -1) (92 . -1) (92 . 1) (92 . -1) (92 . -1))
```

> Именно в таком виде и надо просматривать этот словарь. ... Это распечатка словаря блока, в котором установлен
> только один Visibility Set и больше ничего. Параметр, ручка от него и два объекта - один расположение ручки по X
> и другой - по Y. Если ручку выключить - этих трех объектов не будет, только параметр останется.
> (The right-hand annotations are the author's, to show what each record is.)

## Post #7 by Supermax (13.09.2008) — the master explanation of graph record structure (verbatim)

> Я думаю, что до 96 кода описывать точечные пары не надо, поскольку о них все написано в книгах по автолиспу.
>
> 96 и 97 код пока рассматривать не будем, поскольку без понимания всего в целом, значения эти не понять.
>
> По скольку AutoDeck не дает описания структуы, и даже терминов, которые он присваивает не дает - будем придумывать
> термины свои. ...
>
> Помимо отдельных точечных пар в начале списка, которые несут общее определение имени, типа объекта, метки, и пр.
> все остальное делится на "записи". "Записью" я называю ряд точечных пар, из которых состоит что-то типа
> предложения, описывающего некую константу.
>
> **Начинается запись после 97 кода с точечной пары с номером записи по порядку. Это код 91. Записи маркируются с 0.**
>
> ```
> (91 . 0) (93 . 32) (95 . 11) (360 . <Entity name: 7bf53308>) (92 . 0) (92 . 0) (92 . 1) (92 . 2)
> ```
>
> **Следующая пара с кодом 93 - тип записи.** В этом же словаре есть и другого типа записи и в других словарях тоже
> по всей видимости, а в недрах Автокада есть скорее всего набор функций, которые в зависимости от типа записи
> знают сколько точечных пар в ней, на каких местах кто стоит и что означает, и в соответствии с типом записи она
> и обрабатывает эти записи.
>
> **Следующий код 95 - номер создания элемента по порядку.** То есть, когда создается элемент ему присваивается
> следующий порядковый номер. Этот номер изменить нельзя - фатал еррор сразу. Он сопровождает 360 код, в какую
> строку его не перемести. И скажу сразу, для практических целей совсем бесполезен. На последовательность
> отображения свойств в списке свойств блока не влияет. Но это он тут для манипуляций им бесполезен, но Lookup
> активатор именно на него и ссылается.
>
> **Следующий код 360 - указатель на объект.** Это или ...PARAMETER, или ...GRIP, или BLOCKGRIPLOCATIONCOMPONENT,
> или ...ACTION. То, что в редакторе блоков вы видите. ...
>
> **Следующий код 92 - Первый в строчке с 92 кодами - номер данного объекта в иерархии зависимости-принадлежности,
> или другими словами маркер места в родословной.** ... В объекте ...ACTION в случае управления им цепочкой
> параметров указывает на вторую запись расширенных данных первого в цепочке управляемого параметра.
>
> **Следующий код 92 - Второй в строчке с 92 кодами - дополнительный, второй маркер места.** Служит для указания
> "приемного" родителя которым как правило выступают ...ACTION. Главным родителем всегда выступает ...GRIP. Если у
> объекта нет ACTION, то первая пара 92 и вторая равны. ... В объекте ...ACTION в случае управления им цепочкой
> параметров указывает на вторую запись расширенных данных последнего в цепочке управляемого параметра.
>
> **Следующий код 92 - Третий в строчке с 92 кодами - Указатель на дочерний компонент №1.** Если эта пара
> принадлежит элементу ...PARAMETER, то это указатель на дочерний компонент "UpdatedX", то есть положение по X.
> Если это ...GRIP или ...ACTION, то это указывает на дочерний компонент ...PARAMETER.
>
> **Следующий код 92 - Четвертый в строчке с 92 кодами - Указатель на дочерний компонент №2.** Если эта пара
> принадлежит элементу ...PARAMETER, то это указатель на дочерний компонент "UpdatedY", то есть положение по Y.
> Если это ...GRIP или ...ACTION, то это указывает на дочерний компонент ...PARAMETER.
>
> Исключением является объект LOOKUP, где в случае цепочки LOOKUP-ов эта точечная пара указывает на продолжение
> расширенной записи, а там уже на компонент "UpdatedY".
>
> В случае если это ...GRIP или ...ACTION, последние два 92 кода всегда одинаковые и указывают на одно и то же.
>
> Если в точечной паре стоит код -1, это значит, что данная точечная пара не содержит информации о принадлежности.
>
> **После окончания описания всех свойств записями (93 . 32) идут записи (93 . 0), которые являются расширенными
> данными о принадлежности элементов друг-другу. Эти записи привязаны к основным по коду маркера положения в
> родословной.**
>
> ```
> (92 . 0) (93 . 0) (94 . 1) (91 . 1) (91 . 0) (92 . -1) (92 . -1) (92 . -1) (92 . -1) (92 . -1)
> (92 . 1) (93 . 0) (94 . 1) (91 . 0) (91 . 2) (92 . -1) (92 . -1) (92 . -1) (92 . 2) (92 . -1)
> (92 . 2) (93 . 0) (94 . 1) (91 . 0) (91 . 3) (92 . -1) (92 . -1) (92 . 1) (92 . -1) (92 . -1))
> ```
>
> **Первый код 92 - Указатель на маркер элемента** (первая или вторая точечная пара 92 в строке из четырех
> точечных пар). Это говорит, что данная запись принадлежит именно этому элементу.
>
> **Второй код 93 - тип записи.**
>
> **Третий код 94 - понятия не имею. Всегда 1.**
>
> Далее идут два 91 кода - **Первый - указатель на номер 32-ой записи элемента-родителя. Второй - указатель на
> номер собственной 32-ой записи.**
>
> Далее идут пять 92 кодов -
> **Первый - указатель на основной маркер записи элемента.** ... В дополнительных расширенных данных (вторая
> запись) указывает на дополнительные расширенные данные предыдущего в цепочке элемента.
> **Второй - указатель на дополнительный маркер записи элемента.** ... Если такового нет, то значение -1. В
> дополнительных расширенных данных (вторая запись) указывает на дополнительные расширенные данных следующего в
> цепочке элемента.
> Первая и вторая точечная пара с 92 кодом заполняются значениями только тогда, когда есть "приемный родитель",
> то есть у элемента ...PARAMETER есть связь с элементом ...ACTION. В остальных случаях обе пары всегда -1.
> **Третий** - В расширенной записи "UpdatedY" указатель на связь с "UpdatedX"; В расширенной записи "Lookup"
> указатель на расширенные данные предыдущего в цепочке Lookup-a.
> **Четвертый** - В расширенной записи "UpdatedX" указатель на связь с "UpdatedY"; В расширенной записи "Lookup"
> указатель на расширенные данные следующего в цепочке Lookup-a.
> Третий и четвертый код во всех остальных расширенных данных всегда -1 (во всяком случае, я не обнаружил других
> данных).
> **Пятый** - В случае наличия цепочки связей на связь предыдущей или следующей записи дополнительных расширенных
> данных (2-я запись).
>
> BLOCKVISIBILITYPARAMETER не имеет ACTION. Вообще. Только GRIP и два BLOCKGRIPLOCATIONCOMPONENT.
> ...GRIP не имеет расширенных данных и значения первых двух 92 кодов в 32-ой записи всегда -1.

### English summary of post #7 (the graph grammar)

- After the header (up to and including 97) the body is a sequence of **records**.
- A **main record** starts with `91` (record number, 0-based), `93 . 32` (record type tag "32"), `95` (element
  creation number — immutable; the Lookup activator's 94 points at it), `360` (pointer to the
  PARAMETER/GRIP/LOCATIONCOMPONENT/ACTION object), then four `92` fields:
  - 92 #1 = the element's own "main marker" in the dependency/lineage hierarchy;
  - 92 #2 = "adopted parent" marker (usually an ACTION; equals #1 when there is no action; the main parent is
    always the GRIP);
  - 92 #3 = pointer to child component #1 (for a PARAMETER: its "UpdatedX"; for GRIP/ACTION: the PARAMETER);
  - 92 #4 = pointer to child component #2 (for a PARAMETER: its "UpdatedY"; for GRIP/ACTION: the PARAMETER;
    identical to #3 for GRIP/ACTION; for a LOOKUP chain: the next extended record).
  - `-1` = "no affiliation info".
- After all `93 . 32` records come **extended records** `93 . 0` — "extended data about elements belonging to each
  other", attached to the main records by the lineage marker. Layout of one extended record:
  - 92 = marker of the owning element (first or second 92 of that element's main record);
  - 93 = record type (0 here);
  - 94 = unknown, always 1;
  - 91 #1 = number of the parent element's 32-record; 91 #2 = number of this element's own 32-record;
  - five 92s: #1 main marker (in a 2nd extended record: previous element's extended data), #2 adopted marker
    (-1 if none; in a 2nd extended record: next element's extended data), #3 (UpdatedY→UpdatedX link /
    Lookup-chain previous), #4 (UpdatedX→UpdatedY link / Lookup-chain next), #5 (chain link to previous/next
    extended record). #3/#4 are -1 in all non-Updated/non-Lookup extended records.

## Post #10 by Supermax (14.09.2008) — 96/97 and reordering properties (verbatim)

> Вернемся к 96 и 97 коду в словаре ACAD_EVALUATION_GRAPH.
>
> **Они всегда равны.** Почему их два - не знаю. Показывают оба на номер создания последнего свойства (код
> точечной пары в 32 записи 95). Это тот, который менять нельзя. Если нужно добавить свойство в словарь, чтобы его
> всего не перелопачивать (а в нем ого-го сколько может быть всего) достаточно найти эти коды и прибавив 1 создать
> новую запись.
>
> Теперь поговорим о перестановках свойств местами.
>
> У этого словаря есть одна особенность: Если переставить 32 или 0-вые записи местами, то есть целиком, вместе с
> номером, то после модификации словаря функцией entmod и после прочтения этого словаря заново, мы с удивлением
> обнаружим, что все опять вернулось на свои места. То есть последовательность расположения записей в словаре
> строго по порядку.
>
> Но, если взять и поменять местами только номера 32 записей и везде, где на них есть ссылка, то после entmod и
> повторного прочтения мы увидим, что порядок следования записей строго по возрастанию, но вот начинка этих записей
> поменялась местами.
>
> В списке свойств панели properties это к перемещению свойств местами не приведет. Для того, чтобы поменять
> свойства местами в этой панели, надо после обмена между двумя записями их номерами открыть функцией entget то
> свойство, которое мы перемещаем (любое из двух) и выбросив из него DXF коды, препятствующие проведению entmod
> сделать ему это самое entmod.
>
> Может быть достаточно просто блоку сделать entmod, не пробовал.
>
> Теперь про функцию vla-getdynamicblockproperties: Она дает список свойств блока в той последовательности, в
> которой они отображены в панели properties. Но после наших манипуляций в панели будет одно, а в списке,
> возвращаемом этой функцией - старое. Чтобы и в нем поменялась последовательность расположения свойств, надо
> сделать entmod всему блоку.
>
> Вот и вся технология по перемещению свойств местами.

## Post #12 by Supermax (14.09.2008) — BLOCKVISIBILITYPARAMETER dump (verbatim)

Extraction code used:

```lisp
(mapcar '(lambda (x)
  (if (and (= (car x) 360)
  (= (cdr (assoc 0 (entget (cdr x)))) "BLOCKVISIBILITYPARAMETER"))
  (setq BLC-VIS-PAR (entget (cdr x))))) EVAL_GRAPH)
```

```
((-1 . <Entity name: 7c702330>)
(0 . "ACAD_EVALUATION_GRAPH")
(5 . "20E")
(102 . "{ACAD_REACTORS") (330 . <Entity name: 7c702328>) (102 . "}")
(330 . <Entity name: 7c702328>)
(100 . "AcDbEvalGraph")
(96 . 8)
(97 . 8)
(91 . 0) (93 . 32) (95 . 5) (360 . <Entity name: 7c702340>) (92 . 0) (92 . 0) (92 . 1) (92 . 2)
(91 . 1) (93 . 32) (95 . 6) (360 . <Entity name: 7c702348>) (92 . -1) (92 . -1) (92 . 0) (92 . 0)
(91 . 2) (93 . 32) (95 . 7) (360 . <Entity name: 7c702350>) (92 . 1) (92 . 1) (92 . -1) (92 . -1)
(91 . 3) (93 . 32) (95 . 8) (360 . <Entity name: 7c702358>) (92 . 2) (92 . 2) (92 . -1) (92 . -1)
(92 . 0) (93 . 0) (94 . 1) (91 . 1) (91 . 0) (92 . -1) (92 . -1) (92 . -1) (92 . -1) (92 . -1)
(92 . 1) (93 . 0) (94 . 1) (91 . 0) (91 . 2) (92 . -1) (92 . -1) (92 . -1) (92 . 2) (92 . -1)
(92 . 2) (93 . 0) (94 . 1) (91 . 0) (91 . 3) (92 . -1) (92 . -1) (92 . 1) (92 . -1) (92 . -1))
```

```
((-1 . <Entity name: 7c702340>)
(0 . "BLOCKVISIBILITYPARAMETER")
(330 . <Entity name: 7c702330>)
(5 . "218")
(100 . "AcDbEvalExpr")
(90 . 5)
(98 . 27)
(99 . 1)
(100 . "AcDbBlockElement")
(300 . "Visibility State")
(98 . 27)
(99 . 1)
(1071 . 16)
(100 . "AcDbBlockParameter")
(280 . 1)
(281 . 0)
(100 . "AcDbBlock1PtParameter")
(1010 1121.97 306.361 0.0)
(93 . 6)
(170 . 0)
(171 . 0)
(100 . "AcDbBlockVisibilityParameter")
(281 . 1)
(301 . "Visibility")
(302 . "")
(91 . 0)
(93 . 3)
(331 . <Entity name: 7c702310>) (331 . <Entity name: 7c702318>) (331 . <Entity name: 7c702320>)
(92 . 1)
(303 . "VisibilityState0")
(94 . 3)
(332 . <Entity name: 7c702310>) (332 . <Entity name: 7c702318>) (332 . <Entity name: 7c702320>)
(95 . 0))
```

## Post #13 by Supermax (14.09.2008) — parameter-object code meanings (verbatim)

> Рассотрим эти коды:
>
> Начиная с кода -1 и по код 302 только три кода имеют интерес, это коды 280, 300 и 301.
>
> **280 - видимость параметра Visibility в списке свойств меню Properties. 1 - видим, 0 - нет. (добавлено)**
>
> **300 - наименование параметра Parameter name** из списка свойств объекта Visibility Set. Если открыть редактор
> блока и подсветить ручками объект Visibility Set, то в таблице свойств вы найдете этот параметр и можете его
> отредактировать.
>
> **301 - там же Visibility label.** То, что мы видим в редакторе блоков, написанное на объекте Visibility Set.
>
> **302 - там же Visibility description.** Тоже текстовая строка. Обозначение в списке свойств блока -
> наименования раскрывающегося списка Visibility Set. То, что пишется слева от раскрывающегося списка.
>
> И забегая немного вперед **303 - Наименование представления видимости.** Таких 303 столько, сколько у вас в
> списке представлений.
>
> Все остальные коды до 91 не имеют особого интереса для пионеров по ковырянию блоков.
>
> **91 код - не известно (изменено)**
>
> **93 код (не путать с 93 кодом в начале списка) - количество графических и текстовых элементов в блоке, включая
> и атрибуты.** Общее, так сказать, количество элементов.
>
> За этим кодом следует 331 коды с указателями на сами элементы. Свойств в этом списке нет. Для чего иметь этот
> перечень расскажу позже.
>
> **92 код - количество представлений в данном Visibility Set-e. (исправлено)**
>
> А вот дальше - самое интересное.
>
> **303 код - имя представления (самое первое представление в данном списке имеет №0)**
>
> **94 код - количество элементов в этом представлении**
>
> **332 коды - перечень указателей на те элементы, которые видны в этом представлении.**
>
> **95 код - количество свойств видимых в этом представлении (свойство Visibility Set не считается)**
>
> И если помимо Visibility Set есть еще какие-нибудь свойства, то далее идут 333 коды с указателями на эти
> свойства.
>
> Если представлений много, то запись 303, 94, 332-е, 95, 333-ие повторяется и повторяется.
>
> Если в представлении нет элементов, то есть (94 . 0), то и 332 кодов тоже нет. Если в представлении нет
> свойств, то есть (95 . 0), то и 333 кодов тоже нет.

## Post #14 by Supermax (15.09.2008) — evaluation semantics (verbatim)

> Теперь коротко что и для чего сделано.
>
> **Полный список элементов блока, как и свойств его, нужен, чтобы видимость выключать при переходе из одного
> представления в другое, а список элементов в записи под именем представления - для того, чтобы видимость
> элементам включать. То есть Автокад при каждом переключении видимости сначала все, что в записи с 331 кодами и
> все свойства блока выключает, а затем по записи с 332 кодами и 95-ми следующими после точечной пары с именем
> представления, все, что там указано, включает.**
>
> Я удалял из списка всех элементов и из списка элементов в записи представления отдельный элемент (поменяв при
> этом и значение 93 и 94 точечной пары с количеством элементов на меньшее) и все именно так и работает. То есть
> есть реальная возможность частью элементов блока управлять из одного "BLOCKVISIBILITYPARAMETER", а частью из
> другого и они не будут мешать друг другу (если только в общих списках не будет одинаковых элементов).

## Post #42 by Volodich (24.09.2008) — version incompatibility (verbatim)

> Странно... У меня файл-то открывается, но почему-то ни одно действие с блоком я сделать не могу. Не то что
> видимостью управлять, даже простое растяжение или отзеркаливание не работает. А по видимостям, когда нажимаю на
> ручки, менюшки-то с вариантами выпадают, но при выборе любого варианта он не выбирается.
>
> Попробовал по двойному щелчку. Вылезло сообщение: **"Блок содержит объекты-заместители. Невозможно изменить блок
> в редакторе блоков."**
>
> ...
> AutoCAD 2006 rus.

## Post #45 by Supermax (24.09.2008) — 2006 vs 2007 and combinatorics (verbatim, key lines)

> Volodich, я вообще-то делал блок в 2007 английском каде. **Как ты умудрился открыть его в 2006, да еще и русском?
> Там же другой формат файла!** У меня к сожалению накрылся 2006 кад, так что проверить на нем не могу. Даю вам
> файл в 2004 формате - проверяйте работоспособность.
>
> Подсчитаем, сколько надо описаний видимости для реализации подобного блока с одним Visibility Set. Первая группа
> имеет 7 штук описаний видимости, вторая - 8, третья - 6, и так далее 2, 2, 5, 2, 2, 2, 2.
>
> **Это 7*8*6*2*2*5*2*2*2*2=107520 описаний видимости.** ... А тут 107,5 тысяч! ... Даже по два описания видимости
> вкл\выкл у 10-ти элементов это 1024 описания видимости.
>
> В редакторе блоков вы видите текущий Visibility Set и его элементы. ... Перемещайте моей программой нужный вам
> Visibility в самый верх списка и открывайте блок редактором.

## Post #70 by Supermax (27.09.2008) — Lookup evaluation semantics (verbatim)

> Трепанация Автокада прошла успешно!
>
> Вот примитивный (но не совсем) блок. Две линии. Одна с параметром изменения длинны, другая с поворотом. Меняя
> длинну линии меняется поворот верхней линии. соответствие забито в таблицах Action.
>
> Мне удалось связать Lookup-ы в совместную работу.
>
> ...
>
> **Не просто потянуть за ручку линии и увидеть соответствие, для этого надо установить Dist type в
> LINEARPARAMETER на список (List) и перечислить длины 10, 20, 30, 40, 50. Только тогда можно тянуть за ручку
> спокойно. В остальных случаях если длинна не соответствует значению записанному в Lookup-e поворот будет без
> изменения.** Можно установить значение по умолчанию, чтобы при всех остальных значениях длинны не
> соответствующих таблице поворот был скажем 0, но это не интересно. Сохранение старого значения тоже имеет
> огромную ценность.
>
> **Если у вас в одной таблице забито одна цепочка соответствия, а в другой - другая, то переключение одного
> комплекта значений произойдет при совпадении всех значений в другом комплекте. Короче, как только Lookup
> определяет совпадение данных в строке таблицы, так сразу переключается на эту строку и тянет за собой
> переключение другой таблицы.**

## Post #73 by Supermax (27.09.2008) — Lookup controls Lookup via ONE pair (verbatim)

> Да программка - тьфу. **Я всего ОДНУ точечную пару поменял в нужном месте. Вы понимаете, ОДНУ!** Я очень
> сомневаюсь, что господа разработчики редактора динамических блоков не знали о такой возможности. ... Берешь за
> ручку и тянешь, а вокруг все стремительно меняется. ... Подозреваю, что на самой фирме AutoDesk об этом не все
> даже и знают.

## Post #108 by Supermax (02.10.2008) — full recipe to insert a new Visibility Set parameter (verbatim)

> Скажу по секрету, Visibility вставляет сам Автокад банальным запуском команды
>
> ```
> (command "_BParameter" "V" "L" name_visibility (getpoint "Укажите место расположения Visibility Set") "")
> ```
>
> Так что отправив это выражение в ком. строку и все ок. НО! Если уже есть Visibility Set - ничего не получится.
> А чтобы получилось, надо блок для начала поломать, вставив в словарь "ACAD_EVALUATION_GRAPH" "кривой" Visibility
> Set.
>
> **"Кривой" получается элементарно!** Если уже есть один Visibility Set, то мы его берем, выкидываем из списка
> его DXF кодов -1, 5, 1071, и 1010 коды, заменяем 301 пару с названием данного Visibility на свое название и
> создаем по этому списку (в лиспе это функция entmakex) новый объект. Создаем 360 точечную пару с полученным
> указателем на новый объект, создаем 91 точечную пару с номером новой записи в словаре "ACAD_EVALUATION_GRAPH".
> Добавляем точечную пару "тип записи", создаем и добавляем в запись 95 пару с следующим номером записи, потом
> нашу 360-тую цепляем и далее четыре 92 пары с кодом -1.
>
> **Замеменяем в словаре 96 и 97 пары на новое число (1+) Вставляем нашу запись в конец всех 32-ых записей и делаем
> словарю entmod, после чего сохраняем блок.**
>
> После этого процесса создается ситуация, когда после каждого сохранения блока Автокад в упор не видит Visibility
> Set и дает добро на установку нового. Мы этим воспользуемся и сделаем это.
>
> Затем перемещаем нашу запись с "Кривым" Visibility Set-ом в самый низ 32 записей и удаляем ее.
>
> Блок становится почти корректным. Автокад видит первый по списку Visibility Set и дает его редактировать.
> "Почти" это в смысле, что много Visibility Set-ов блоку де-юре не положено.

### English summary of the insertion recipe

1. Take an existing Visibility Set parameter object; drop codes -1, 5, 1071, 1010 from its DXF; rename it
   (change the 301 pair); create a new object with `entmakex`.
2. In the graph dictionary: add a `360` pair pointing at the new object; add a `91` pair with the new record
   number; add the record-type pair (93 . 32); add a `95` pair with the next creation number; append the 360 pair
   and four `92 . -1` pairs.
3. Update `96` and `97` to the new count (+1); insert the record at the end of the 32-records; `entmod` the
   dictionary; save the block.
4. After saving, AutoCAD no longer sees the "clumsy" Visibility Set and accepts installation of a real new one
   via `_BParameter`.
5. Move the dummy record to the bottom of the 32-records and delete it. The block becomes "almost" correct:
   AutoCAD edits the first-in-list Visibility Set; multiple Visibility Sets are not officially allowed.

## Posts #209 / #444 / #459 — 2010 behavior (verbatim)

Post #209 by Supermax (06.10.2008, updated for 2010):

> Установка дополнительных активаторов дает примерно такие блоки см. пост #212. ...
>
> **К сожалению, в 2010 каде, а может и в 2011 (не проверял) активаторы, в которых не заполнены таблицы, в
> редакторе блока не видны (хотя они есть на самом деле) их заполнение пока можно делать в кадах предыдущих годов
> выпуска или попробовать эксель-лукуп.**

Post #444 by Supermax (02.02.2010): "Внимание! Очередное обновление! Скачайте заново и замените файл
visibility-ADD2.fas. Также надо обновить программу по перемещению свойств." (2010 re-release of the toolkit.)

Post #459 by kor99 (15.12.2010): in 2010, an empty-table Lookup activator can be found via Quick Select on
"Lookup Action -> Action name" with a higher number (e.g. "Lookup2").

## Posts #384 / #385 — 2010 format statements (verbatim)

Post #384 by Supermax (30.01.2009):

> **Формат файла в 2010 каде, на сколько я знаю - 2007. Так что принципиальных изменений ни в структуре
> динамических блоков, ни в их возможностях нет.** Вот сервис может и развили немного. Но опять же дополнительных
> Visibility Set там не появилось (увы). Можно сказать прямо, сдвигов нет.
>
> Николай Николаевич его ковырял, он может вам больше рассказать.

Post #385 by **Н.Н.Полещук** (the site owner, 30.01.2009):

> **Каждые три версии Autodesk меняет формат DWG. Так что в 2010 будет уже новый формат. Программы Supermax'a
> работают в бета-версии 2010.**
>
> **В 2010 версии появятся новшества в динамических блоках, но говорить об этом пока рано.**

## Post #450 by VVA (24.06.2010) — R12 DXF destroys dynamic blocks (verbatim)

> Eddicordo, Открой и сохрани в Авкаде 2004. Сохрани как dxf R12. **Все динамические блоки заменятся на анонимные
> с именами *Unnn.** Дальше, если нужно, можно использовать UX или U2B

## Posts #461–468 (Dec 2010) — BLOCKSTRETCHACTION dumps and entmod procedure

Post #463 by 5hev (22.12.2010) — three full BLOCKSTRETCHACTION dumps (verbatim, first in full):

```
((-1 . <Entity name: 7eb8c1f8>)
(0 . "BLOCKSTRETCHACTION")
(330 . <Entity name: 7eb8c1b8>)
(5 . "35F")
(100 . "AcDbEvalExpr")
(90 . 5)
(98 . 27)
(99 . 25)
(100 . "AcDbBlockElement")
(300 . "Stretch")
(98 . 27)
(99 . 25)
(1071 . 0)
(100 . "AcDbBlockAction")
(70 . 0)
(71 . 1)
(330 . <Entity name: 7eb8c1a8>)
(1010 4.25338 20.7466 0.0)
(100 . "AcDbBlockStretchAction")
(92 . 1)
(301 . "EndXDelta")
(93 . 1)
(302 . "EndYDelta")
(72 . 2)
(1011 -5.0 5.0 0.0)
(1011 13.4784 40.3757 0.0)
(73 . 1)
(331 . <Entity name: 7eb8c1a8>)
(74 . 1)
(94 . 1)
(75 . 0)
(140 . 1.0)
(141 . 0.0)
(280 . 0))
```

(Second and third dumps in the post are the same shape with `71 . 2`, two 330 pairs, two 1011 pairs, `73 . 2`,
two 331/74/94 triplets — i.e. a stretch action affecting two objects. `*` in the post: "зона влияния подразумевает
рамку которую acad просит обвести перед указанием объектов для набора в actionset" — the 1010/1011 pairs describe
the action's influence window.)

Post #464 by Supermax (22.12.2010) — how to add an object to a stretch action (verbatim):

> Тебе надо добавить свой новый элемент в этот список
>
> ```
>  (71 . 2)
> (330 . <Entity name: 7eb8c458>)
> (330 . <Entity name: 7eb8c460>)
> ```
>
> увеличив 71 пару на 1
>
> и добавить в конец
>
> ```
>  (331 . <Entity name: 7eb8c328>)
> (74 . 1)
> (94 . 1)
> ```
>
> увеличив 73 пару на 1
>
> **пары 1010 и 1011 надо просто выкинуть перед entmod-ом. Они сами восстановятся**

Post #465 by 5hev (22.12.2010): tried swapping the 330 pointer to another line; dropped all dotless pairs (1010,
1011) and 1071 — "entmod выдал nil".

Post #466 by Supermax: "Попробуй просто выкинуть 1010, 1011 и 1071, а потом ентмод. Если модифицируется, то есть
вернет список с парами вместо нил, то ты где-то что-то пропустил, а если опять нил, то надо будет искать другие
пути."

Post #467 by 5hev (23.12.2010):

> Оказалось, что **1011 неразрывно связана с 72 парой, которая считает их количество. Порезал 72-ую - и все
> работает!** ... Кстати, можно включить в set любой объект даже вне блока, entmod съест, но вот двигаться объект
> не будет...

Post #468 by Supermax: "А пара 1011 восстанавливается после ентмод? И на что она указывает, разобрался?"

### English summary (action-object grammar, from posts #461–468)

- AcDbEvalExpr header of an action: `90` = the element's creation number (its 95 in the graph record), `98 . 27`
  and `99 . 25/1` = sub-record type tags (27 = AcDbBlockElement, 25 = AcDbBlockAction), `1071` = another tag
  (0 for actions).
- `AcDbBlockAction` section: `70` (0), `71` = number of affected objects, `330` × 71 = pointers to the affected
  entities, `1010` = action origin/position.
- `AcDbBlockStretchAction` section: `92 . 1` + `301`/`93 . 1` + `302` = X/Y displacement value names
  ("EndXDelta"/"EndYDelta"); `72` = count of `1011` pairs; `1011` × 72 = influence-window corner points; `73` =
  count of affected-object entries; `331`/`74`/`94` triplets per affected object (74 . 1 = flag, 94 = ?);
  `75 . 0`; `140 . 1.0` (a double, 1.0); `141 . 0.0`; `280 . 0`.
- **entmod procedure for action objects**: strip `1010`, `1011`, `1071` before `entmod` — they are regenerated
  afterwards; `1011` count must match the `72` pair; `1071` is rejected by entmod if left in.

## Post #501 by VVA (25.05.2012) — entmod broken in 2012 SP2 (verbatim)

> Да я тоже проверил в 2012 SP2 - не работает. Причем трассировка показала, что **не работает ф-ция entmod. Т.е.
> если в 2-х словах то она не обновляет список для объекта (0 . "BLOCKLOOKUPACTION").** С чем это связано - не
> понятно, то ли баг Автокада, то ли такая политика. Если у кого стоит Автокад 2013 - проверьте работу #495

## Post #531 by Astartes (08.06.2013) — 2012/2013 regressions (verbatim)

> Обнаружил неприятную штуку, когда попытался в 2013 каде подредактировать блок с доп. визибли сетами созданный в
> 2011 каде.
>
> **Я конечно знал, что начиная с 2012 каде, уже начались глюки, нельзя создать новый доп. визибли сет, но править
> вроде было можно. А теперь выяснилось что и править блоки с сетами желательно до 2011 када включительно.**
>
> ...Если коротко то, доп. сеты стали сбрасываться от действия другого сета.

## Post #532 by Нефтепроводчик (08.06.2013) — 2010 property tables (verbatim)

> [Shoorup] Еще один из вариантов автоматизации это таблицы, **но они будут работать только с 2010 акада.**

## Post #528 / #708 (2019) — toolkit version compatibility (verbatim, key lines)

> ...файл fas из шапки **работает полностью только в версиях 2010-2011 (в последующих можно только использовать
> результаты работы)**. ... Есть также Lisp-вариант (https://forums.autodesk.com/t5/dynamic-blocks/...), работающий
> в более поздних версиях (в теме говорится про 2016).

## Later user reports (verbatim, key lines)

- (2018, post ~#598): "Пример в шапке есть, но повторить не могу, потому что **autocad не присоединяет вторую
  таблицу свойств к одному параметру lookup, autocad пишет: Lookup parameter already associated with a lookup
  table**."
- (2019, post ~#682): blocks built with the fas in 2011 "работают во всех версиях вплоть до 2018".
- (2020, post ~#708): "в версии 2020 сделал блок формата листа (таблица свойств блока и 2 группы видимостей -
  основная надпись и боковая) ... нужно соблюдать порядок изменения видимостей (сначала меняется последняя,
  добавленная через lisp, первая зависимость сбрасывается на начальный вид, которая потом настраивается)".
- (2024, post ~#740): "работаю в 2010ом Акаде, правда на 64-х битной системе" — the fas still used in 2010.
- (2024, post ~#745): "Вчера пробовал в 2010 создавать, приложение загрузил. Ввожу команды пишет nil nil nil либо
  неизвестная, в 2017 фатальные ошибки."

## Searched but NOT found in this thread

- **No occurrence of DBL_MAX, 1.797693134862314E+38, or any "sentinel value" discussion** (grep across all 717
  posts for `1.797693`, `E+38`, `DBL_MAX`, `MAXFLOAT` → only an unrelated "максимально грамотно" hit).
- No explicit explanation of codes `98`, `99`, `1071`, `140`, `141`, `170`–`174` beyond the dumps above (the
  thread shows their values but the authors never define 98/99/1071; 94 in extended records is "понятия не имею.
  Всегда 1").

===============================================================================
===============================================================================

# 8. adn-cis.org forum topic 1069 — "как получить в net значение пары dxf"

URL: https://adn-cis.org/forum/index.php?topic=1069.0 (HTTP 200, clean)
Board: AutoCAD .NET API. Poster: andrey_ma (10-11-2014, 22:06). Read 10861 times.

## Question (verbatim)

> К примеру есть ObjId и хочу получить чтото в виде:
>
> ```
> Command: (setq ACAD_EVALUATION_GRAPH (entget (cdr (assoc 360 dictionary ))))
> ((-1 . <Entity name: 7ee74610>) (0 . "ACAD_EVALUATION_GRAPH") (5 . "54A") (102 . "{ACAD_REACTORS")
> (330 . <Entity name: 7ee74608>) (102 . "}") (330 . <Entity name: 7ee74608>) (100 . "AcDbEvalGraph")
> (96 . 183) (97 . 183) (91 . 0) (93 . 32) (95 . 175) (360 . <Entity name: 7ee74680>) (92 . 2) (92 . 5)
> (92 . 0) (92 . 7) (91 . 1) (93 . 32) (95 . 176) (360 . <Entity name: 7ee74688>) (92 . -1) (92 . -1) (92 . 2)
> (92 . 2) (91 . 2) (93 . 32) (95 . 177) (360 ...
> ```

### DXF dump 7 — ACAD_EVALUATION_GRAPH of a large real-world block (183 records; first 9 main records verbatim)

```
((-1 . <Entity name: 7ee74610>)
(0 . "ACAD_EVALUATION_GRAPH")
(5 . "54A")
(102 . "{ACAD_REACTORS") (330 . <Entity name: 7ee74608>) (102 . "}")
(330 . <Entity name: 7ee74608>)
(100 . "AcDbEvalGraph")
(96 . 183)
(97 . 183)
(91 . 0) (93 . 32) (95 . 175) (360 . <Entity name: 7ee74680>) (92 . 2) (92 . 5) (92 . 0) (92 . 7)
(91 . 1) (93 . 32) (95 . 176) (360 . <Entity name: 7ee74688>) (92 . -1) (92 . -1) (92 . 2) (92 . 2)
(91 . 2) (93 . 32) (95 . 177) (360 . <Entity name: 7ee74690>) (92 . 3) (92 . 3) (92 . -1) (92 . -1)
... [records 91 . 3 .. 91 . 8 in the same shape; 95 runs 175..183]
```

Observations: a real production block has **183 graph records** (96 = 97 = 183; the 95 creation numbers of the
first records are 175–183, i.e. the block also contains many other objects created earlier). Record [0] has
`92 = (2, 5, 0, 7)`: main marker 2, adopted-parent marker 5 (an ACTION), child #1 = record 0 (self/UpdatedX),
child #2 = record 7.

## DXF dump 8 — BLOCKLINEARPARAMETER (verbatim, complete)

```
((-1 . <Entity name: 7ee74680>)
(0 . "BLOCKLINEARPARAMETER")
(330 . <Entity name: 7ee74610>)
(5 . "54C")
(100 . "AcDbEvalExpr")
(90 . 175)
(98 . 31)
(99 . 8)
(100 . "AcDbBlockElement")
(300 . "Linear")
(98 . 31)
(99 . 8)
(1071 . 0)
(100 . "AcDbBlockParameter")
(280 . 1)
(281 . 0)
(100 . "AcDbBlock2PtParameter")
(1010 -466.407 107.957 0.0)
(1011 -368.502 58.2298 0.0)
(170 . 4)
(91 . 179)
(91 . 176)
(91 . 0)
(91 . 0)
(171 . 1)
(92 . 179)
(301 . "DisplacementX")
(172 . 1)
(93 . 179)
(302 . "DisplacementY")
(173 . 1)
(94 . 176)
(303 . "DisplacementX")
(174 . 1)
(95 . 176)
(304 . "DisplacementY")
(177 . 0)
(100 . "AcDbBlockLinearParameter")
(305 . "Distance1")
(306 . "")
(140 . -13.0145)
(307 . "")
(96 . 1)
(141 . 0.0)
(142 . 0.0)
(143 . 0.0)
(175 . 0))
```

### Interpretation (from the values shown; the thread itself does not define the codes)

- `90 . 175` — the element's creation number, equal to the `95` of its graph record (record [0] has 95 . 175).
- `98 . 31` / `99 . 8` — sub-record type tags: 31 = AcDbBlockElement, 8 = AcDbBlock2PtParameter (vs. 27/1 for the
  Visibility parameter and 27/25 for the Stretch action in the dwg.ru dumps).
- `1071 . 0` — 0 for this parameter (16 for the Visibility parameter, 8 for the Lookup action).
- `100 . "AcDbBlock2PtParameter"`: `1010` = start point, `1011` = end point of the linear parameter.
- `170 . 4` — parameter type/variant tag (0 for the Visibility parameter).
- `91 . 179`, `91 . 176`, `91 . 0`, `91 . 0` — **four 91 pairs carrying record/creation numbers** (179, 176, 0, 0):
  these are references into the graph (record numbers of the related elements), not the "current state" 91 seen in
  BLOCKVISIBILITYPARAMETER — i.e. code 91 is overloaded: in a parameter body it indexes graph records.
- `171 . 1` + `92 . 179` + `301 . "DisplacementX"` — 171 = flag (1 = present), followed by a 92 reference (179) and
  a 301 value-name: the X displacement value.
- `172 . 1` + `93 . 179` + `302 . "DisplacementY"` — the Y displacement value.
- `173 . 1` + `94 . 176` + `303 . "DisplacementX"` — second X displacement (reference 176).
- `174 . 1` + `95 . 176` + `304 . "DisplacementY"` — second Y displacement.
  (So 171–174 are "value-slot" headers: flag + a 9x graph reference + a 30x name; the 9x codes 92–95 here are
  references to graph records, again showing the 9x codes are overloaded between the graph and the parameter bodies.)
- `177 . 0` — another flag.
- `100 . "AcDbBlockLinearParameter"` (the type-specific section):
  - `305 . "Distance1"` — the parameter's display label ("Distance1");
  - `306 . ""` — (empty string, a description/second label);
  - **`140 . -13.0145` — the current double value of the parameter** (the line's length);
  - `307 . ""` — (empty string);
  - `96 . 1` — count of the following `141`/`142`/`143` triples;
  - `141 . 0.0`, `142 . 0.0`, `143 . 0.0` — one triple of doubles (0.0, 0.0, 0.0) — an associated value set
    (e.g. the displacement components);
  - `175 . 0` — a flag (0).

## Question/answer (verbatim, key parts)

> ... как получить в net значение пары dxf ... [the poster wants to read these pairs from a .NET
> `ObjectId`/`ObjectID` without AutoLISP]

Answer in the thread: the AutoCAD .NET API does **not** expose the AcDbEvalExpr sub-records — you must P/Invoke
the unmanaged `acdbEntGet` (the thread links to adn-cis.org forum topic 1029 "P/Invoke acdbEntGet" and shows the
`DllImport("accore")` pattern with `AcDbObjectId` and a `CHAR_BUFFER`/`AcDbDxf` result structure).

===============================================================================

# 9. adn-cis.org "poisk-sosednix-komnat.html"

URL: https://adn-cis.org/poisk-sosednix-komnat.html (HTTP 200, clean)

**NOT related to AcDbEvalGraph.** The page is a Revit API article ("Поиск соседних комнат" — "Finding adjacent
rooms", a Russian translation of a The Building Coder post by Erik GiGi/Eriksson) about finding adjacent rooms in
a Revit model (walking `Document`, `FilteredElementCollector` for `Room` elements, comparing bounding boxes /
shared walls). It contains no AutoCAD dynamic-block content, no AcDbEvalGraph/AcDbEvalExpr mention, no DXF dumps.
It is listed here only to record that it was fetched and is a red herring for this research topic.
(It does link to an ADN discussion: http://adn-cis.org/forum/index.php?topic=204 — also Revit.)

===============================================================================

# 10. forum.abok.ru topic 14612

URL: https://forum.abok.ru/index.php?showtopic=14612

**UNREACHABLE.** The forum sits behind a DDoS-Guard JavaScript challenge:
- `web_fetch` → HTTP 403 "DDoS-Guard: Checking your browser before accessing".
- `mcp__mcp-searxng__web_extract_webpageinformations` → tool error.
- `mcp__mcp-searxng__web_webpage_download` → tool error.
- `curl` with full browser headers → HTTP 403 (the JS-challenge HTML, 902 bytes).
- Wayback Machine: `archive.org/wayback/available` → HTTP 429 (rate-limited); the CDX query timed out.

What is known about the thread (from search results, NOT from the thread itself):
- It is a long thread (at least 540+ pages/posts; a dwg.ru post references `showtopic=14612&st=540`).
- dwg.ru thread 14519 "Эмуляция нажатия клавиш из под AutoLisp-a" (14.12.2014) quotes it: "Начинаю я от сюда и
  дальше http://forum.abok.ru/index.php?showtopic=14612&st=540. Все 'умники', которые считают, что стоит только
  захотеть и работать с динамическими..." — i.e. the thread is about **working with dynamic blocks** (and
  emulating key presses to drive the block editor).
- dwg.ru thread 12118 (3D heating-pipe design) also references it.

**No content of the abok.ru thread could be retrieved; nothing below is quoted from it.**

===============================================================================

# 11. Cross-source synthesis

## 11.1 The AcDbEvalGraph (DXF class ACAD_EVALUATION_GRAPH)

- A dictionary entry (0 . "ACAD_EVALUATION_GRAPH") owned by the block's extension DICTIONARY, which also contains
  "ACAD_ENHANCEDBLOCK" and "AcDbDynamicBlockRoundTripPurgePreventer".
- Header: `100 . "AcDbEvalGraph"`, then `96` and `97` — **always equal** — the number of main (32-type) records.
- Body: `96` main records, each:
  - `91` — record number, 0-based; records are always ordered by this number (swapping whole records is reverted
    by entmod; swapping only the 91 values swaps record contents).
  - `93 . 32` — record type tag for a main record (other 93 values exist in other dictionaries; AutoCAD has
    internal functions that know, per record type, how many pairs a record has and what each position means).
  - `95` — the element's **creation number**: assigned at creation, immutable (changing it → fatal error), always
    travels with the `360` pair; has no effect on Properties-palette order, but **the Lookup activator's 94 pair
    points exactly at it**.
  - `360` — pointer to the element object: a `BLOCK*PARAMETER`, `BLOCK*GRIP`, `BLOCKGRIPLOCATIONCOMPONENT`
    ("UpdatedX"/"UpdatedY"), or `BLOCK*ACTION`.
  - four `92` fields (see post #7 above): main marker, adopted-parent marker (ACTION; -1 if none; the main parent
    is always the GRIP), child #1 (UpdatedX for a parameter / the parameter for a grip-action), child #2
    (UpdatedY / the parameter; identical to #3 for grip/action; Lookup-chain continuation for a LOOKUP).
- Then the **extended records** `93 . 0` (one per main record, in the same order): 92 (owning marker), 93 (0),
  94 (unknown, always 1), 91 #1 (parent's 32-record number), 91 #2 (own 32-record number), five 92s (main marker;
  adopted marker or -1; UpdatedY↔UpdatedX link / Lookup-chain prev; UpdatedX↔UpdatedY link / Lookup-chain next;
  chain link to prev/next extended record).
- `96`/`97` must be incremented when a record is added; the new record is appended at the end.

## 11.2 The AcDbEvalExpr elements (subclasses)

Common header: `100 . "AcDbEvalExpr"`, `90` (= the element's creation number = the 95 of its graph record), then
sub-records each prefixed by a `98` type tag and a `99` tag:

| 98 value | sub-record class | seen in |
|---|---|---|
| 27 | AcDbBlockElement | Visibility param, Lookup action |
| 31 | AcDbBlockElement | Linear (2-pt) parameter |
| 25 | AcDbBlockAction | Stretch action |
| 8  | (2-pt parameter section) | Linear parameter |

- `1071` — per-record tag (values observed: 0 actions/linear param, 8 Lookup action, 16 Visibility parameter).
  **Rejected by entmod — must be stripped before entmod; regenerated afterwards.**
- `AcDbBlockElement` section: `300` = element/parameter name.
- `AcDbBlockParameter` section: `280` = visibility of the parameter in the Properties palette (1/0); `281` (flag).
- `AcDbBlock1PtParameter`: `1010` = the single point.
- `AcDbBlock2PtParameter`: `1010` = start point, `1011` = end point.
- `AcDbBlockVisibilityParameter`: `281 . 1`, `301` = Visibility label, `302` = Visibility description, `91` =
  current state index (0-based; "unknown (amended)" per post #13), `93` = total block element count, `331` × 93 =
  all elements, `92` = number of states, then per state: `303` = state name, `94` = element count in the state,
  `332` × 94 = elements visible in the state, `95` = property count in the state, `333` × 95 = visible
  properties. (If 94 . 0 → no 332; if 95 . 0 → no 333.)
- `AcDbBlockLookupAction`: `92 . 0` (first column index?), `93 . 1` (column count?), `301 . ""`, `303 . ""`,
  `94 . 5` (**points at the 95 creation number of the parameter the activator is bound to**), `95 . 1`,
  `96 . 0`, `282 . 1`, `305 . "Custom"`, `281 . 0`, `304 . "lookupString"`, `280 . 1`.
- `AcDbBlockStretchAction`: `92 . 1`+`301`/`93 . 1`+`302` (X/Y value names, "EndXDelta"/"EndYDelta"), `72` = count
  of `1011` influence-window points, `1011` × 72, `73` = count of affected objects, `331`/`74`/`94` triplets,
  `75 . 0`, `140 . 1.0`, `141 . 0.0`, `280 . 0`.
- `AcDbBlockLinearParameter`: `305` = label ("Distance1"), `306`/`307` = strings, **`140` = the current double
  value**, `96` = count of `141`/`142`/`143` value triples, `175` = flag.
- `170`–`174` (linear parameter): `170 . 4` = type tag; `171`–`174` = value-slot headers, each = flag (1) + a 9x
  graph-record reference + a 30x value name ("DisplacementX/Y" ×2).
- `177`, `175` = flags (0).

## 11.3 Evaluation semantics (what AutoCAD does)

- **Visibility switching**: on every state switch AutoCAD first turns OFF all elements listed in the 331 list
  (all block elements) and all block properties, then turns ON the elements of the 332 list and the properties of
  the 95/333 lists of the selected state (per the 303 state-name pair).
- **Grip move / parameter change**: the graph is a dependency graph (parameters ← grips ← actions). A grip's
  UpdatedX/UpdatedY location components are linked through the 92 "adopted parent" fields; actions reference their
  target parameters via the 94→95 link. Moving a grip updates the parameter values, which in turn drives the
  actions on dependent elements.
- **Lookup**: a Lookup parameter is a table. **As soon as the current values of the linked parameters match a row
  of the table, the Lookup switches to that row** and sets all the parameters of that row (including other
  Lookups — chained Lookups switch on matching all values of the other set). A non-matching value leaves the old
  value in place (a default value can be set for non-matching cases). One Lookup parameter can carry any number of
  activators (tables); Autodesk's UI only allows one. A Lookup can be made to control another Lookup by changing
  ONE dotted pair.
- **Ordering**: the Properties palette order = the 91 record order in the graph (after entmod on the parameter and
  the block). The `vla-getdynamicblockproperties` cache is refreshed only by entmod on the block.
- **Block-editor ↔ graph mapping**: entget on a block-editor element returns only `(-1 . <Entity>)`; the -1 handle
  equals the 331/332 handles; graph 360 handles equal the 333 handles of BLOCKVISIBILITYPARAMETER; the (ssget "_X")
  list and the graph's parameter list are identical in order.

## 11.4 entmod / modification rules (empirical)

- entmod on the graph dictionary: strip `1071` and `1010` from parameter objects first; they are regenerated.
- entmod on the graph reorders records by 91 (whole-record swaps are reverted; 91-value swaps stick).
- To make palette order change: entmod the moved parameter (minus 1071/1010) AND entmod the block.
- Actions: strip 1010/1011/1071 before entmod; 1011 count must equal the 72 pair; 1011 is regenerated.
- 2012 SP2: entmod no longer updates the BLOCKLOOKUPACTION list (bug or policy, unknown).

## 11.5 Version history (statements actually found)

| Version | Statement (source) |
|---|---|
| 2006 | Cannot open 2007 dynamic blocks: "Блок содержит объекты-заместители. Невозможно изменить блок в редакторе блоков." (dwg.ru #42); 2006/2007 file formats differ (#45) |
| 2007 | All Lazebny/dwg.ru dumps are 2007-era (AutoCAD 2007, version string "17.0s (LMS Tech)") |
| 2009-01-30 | "Формат файла в 2010 каде, на сколько я знаю - 2007. ... принципиальных изменений ни в структуре динамических блоков, ни в их возможностях нет" (Supermax, #384); "Каждые три версии Autodesk меняет формат DWG. Так что в 2010 будет уже новый формат. ... В 2010 версии появятся новшества в динамических блоках, но говорить об этом пока рано" (N.N. Polischuk, #385) |
| 2010 | New dynamic element: **property tables** ("таблицы свойств") — "будут работать только с 2010 акада" (#532); Lazebny v1.4 (11.02.10) adds support for properties tables; empty-table Lookup activators invisible in the 2010 block editor (#209); Supermax re-released the toolkit 02.02.2010 "в связи с появлением нового динамического элемента в 2010 каде" (#1) |
| 2011 | The fas toolkit "работает полностью только в версиях 2010-2011" (#708); multi-visibility blocks built in 2011 work in all versions up to 2018 |
| 2012 | Regressions: "начиная с 2012 каде, уже начались глюки, нельзя создать новый доп. визибли сет, но править вроде было можно" (#531); entmod broken for BLOCKLOOKUPACTION in 2012 SP2 (#501) |
| 2013 | "и править блоки с сетами желательно до 2011 када включительно ... доп. сеты стали сбрасываться от действия другого сета" (#531) |
| 2016 | Toolkit update by DBdJ1 "can work in AutoCAD 2016" (Lazebny part 12, 22.10.15) |
| 2017/2020/2024 | Users still using the fas in 2010 (64-bit); in 2017 "фатальные ошибки"; 2020: sheet-format block (property table + 2 visibility groups) works with a state-switching-order caveat |

**DBL_MAX / 1.797693134862314E+38 sentinel: NOT mentioned in any retrieved source.** (Searched all 717 dwg.ru
posts and all Lazebny parts — no hit.)

===============================================================================

# 12. Sources used

| # | URL | Result | What was obtained |
|---|-----|--------|-------------------|
| 1 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod07e.htm | OK (200) | Part 7 English, complete |
| 2 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod08e.htm | OK (200) | Part 8 English, complete (incl. BLOCK_RECORD, DICTIONARY, ACAD_EVALUATION_GRAPH, BLOCKVISIBILITYPARAMETER dumps) |
| 3 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod09e.htm | OK (200) | Part 9 English, complete (Lookup graph + BLOCKLOOKUPACTION dumps) |
| 4 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod10e.htm | OK (200) | Part 10 English, complete |
| 5 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod11e.htm | OK (200) | Part 11 English, complete (webmacro history, off-topic) |
| 6 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod12e.htm | OK (200) | Part 12 English (appendix: 12 functions, version history) |
| 7 | http://poleshchuk.spb.ru/cad/2009/tainypod07.htm | OK (200) | Russian part 7 — raw bytes koi8-r; web_fetch shows mojibake, curl+koi8-r decode = clean full text |
| 8 | http://poleshchuk.spb.ru/cad/2009/tainypod08.htm | OK (200) | Russian part 8, clean koi8-r decode (same dumps as English) |
| 9 | http://poleshchuk.spb.ru/cad/2009/tainypod09.htm | OK (200) | Russian part 9, clean |
| 10 | http://poleshchuk.spb.ru/cad/2009/tainypod10.htm | OK (200) | Russian part 10, clean |
| 11 | http://poleshchuk.spb.ru/cad/2009/tainypod11.htm | OK (200) | Russian part 11, clean |
| 12 | http://poleshchuk.spb.ru/cad/2009/tainypod12.htm | OK (200) | Russian part 12, clean (version history incl. 2010 property tables) |
| 13 | http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod07.htm | OK (200) | Russian part 7 on 2nd mirror — identical content, also koi8-r |
| 14 | https://forum.dwg.ru/showthread.php?t=24597 (pages 1–36) | OK (200 × 36) | All 717 posts extracted (cp1251); raw HTML in dwg_raw/, text in dwg_full_thread.txt |
| 15 | https://forum.abok.ru/index.php?showtopic=14612 | BLOCKED (403 DDoS-Guard JS challenge via web_fetch, mcp extract, mcp download, curl) | Nothing. Wayback: 429 rate-limit / timeout. Only external references found via search (thread is about dynamic blocks, 540+ posts) |
| 16 | https://adn-cis.org/poisk-sosednix-komnat.html | OK (200) | Revit room-adjacency article — UNRELATED to AcDbEvalGraph (red herring) |
| 17 | https://adn-cis.org/forum/index.php?topic=1069.0 | OK (200) | .NET question thread with 183-record ACAD_EVALUATION_GRAPH dump and complete BLOCKLINEARPARAMETER dump |
| 18 | mcp__mcp-searxng__web_search (abok.ru "showtopic=14612") | OK | External references to the abok.ru thread (dwg.ru 14519, 12118) |
| 19 | mcp__mcp-searxng__web_extract_webpageinformations (abok.ru) | FAILED (tool error) | — |
| 20 | mcp__mcp-searxng__web_webpage_download (abok.ru) | FAILED (tool error) | — |
| 21 | http://archive.org/wayback/available + web.archive.org CDX (abok.ru) | FAILED (429 / timeout) | — |

Local artifacts (in .tmp_research/): en_part07..12.html/.txt (English Lazebny parts), ru_part07..12.html (Russian
raw), ru_extracted.txt (clean Russian text), dwg_raw/p01..p36.html (forum pages), dwg_full_thread.txt (all 717
posts), dwg_p1-4.txt (first 80 posts), extract_dwg.py / download_all_dwg.py / extract_ru.py (scripts).
