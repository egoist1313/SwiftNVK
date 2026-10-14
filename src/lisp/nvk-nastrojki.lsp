;;; nvk-nastrojki.lsp — DCL настроек участка
;;; Зависит от: nvk-config, nvk-nod.

(setq *nvk-nastrojki-dcl*
"nvk_nastrojki : dialog {
  label = \"Поперечник НВК — настройки\";
  : boxed_column {
    label = \"Траншея\";
    : edit_box { label = \"Постель, м\"; key = \"bedding\"; edit_width = 8; }
    : edit_box { label = \"Защитный слой, м\"; key = \"zash\"; edit_width = 8; }
    : edit_box { label = \"Одежда ASPH, м (0=нет)\"; key = \"asph\"; edit_width = 8; }
    : edit_box { label = \"Газон, м (0=нет)\"; key = \"gazn\"; edit_width = 8; }
    : edit_box { label = \"Тротуар, м (0=нет)\"; key = \"trot\"; edit_width = 8; }
    : popup_list { label = \"Стык\"; key = \"joint\"; edit_width = 16; }
    : edit_box { label = \"Откос m\"; key = \"slope\"; edit_width = 8; }
    : edit_box { label = \"Шаг уровней CS, ед.\"; key = \"ygap\"; edit_width = 8; }
    : toggle { label = \"Проект / факт\"; key = \"proj_fact\"; }
    : toggle { label = \"Однострочный scatter\"; key = \"single_sc\"; }
    : toggle { label = \"Аннотативный текст\"; key = \"annot\"; }
    : toggle { label = \"Одежда на всю трассу\"; key = \"pav_all\"; }
    : toggle { label = \"Зоны кликом после ввода\"; key = \"pick_z\"; }
    : edit_box { label = \"Зоны А:от-до Г:от-до\"; key = \"zones\"; edit_width = 24; }
    : edit_box { label = \"Интервал выносок плана, м\"; key = \"plan_int\"; edit_width = 8; }
  }
  : boxed_column {
    label = \"Масштабы\";
    : edit_box { label = \"Профиль H (1 м = N ед.)\"; key = \"h_sc\"; edit_width = 8; }
    : edit_box { label = \"Профиль L (1 м = N ед.)\"; key = \"l_sc\"; edit_width = 8; }
    : edit_box { label = \"Сечение (1 м = N ед.)\"; key = \"cs_sc\"; edit_width = 8; }
  }
  : boxed_column {
    label = \"Станции поперечников\";
    : edit_box { label = \"AUTO или пикеты через запятую\"; key = \"stations\"; edit_width = 24; }
  }
  : boxed_column {
    label = \"Графика\";
    : toggle { label = \"Сечения блоками\"; key = \"block_cs\"; }
    : toggle { label = \"Продольники блоками\"; key = \"block_ep\"; }
    : toggle { label = \"Подписи L0-L6\"; key = \"show_tags\"; }
    : edit_box { label = \"Полуширина TIN, м\"; key = \"hwidth\"; edit_width = 8; }
  }
  : boxed_column {
    label = \"Таблица (PoperSwift)\";
    : edit_box { label = \"Высота текста профиля\"; key = \"text_h\"; edit_width = 8; }
    : edit_box { label = \"Размер текста сечений\"; key = \"cs_th\"; edit_width = 8; }
    : edit_box { label = \"Отступ текста, %\"; key = \"text_off\"; edit_width = 8; }
    : popup_list { label = \"Единица уклона\"; key = \"slope_u\"; edit_width = 16; }
    : toggle { label = \"Знак промилле\"; key = \"show_perm\"; }
    : edit_box { label = \"Ширина столбца (0=авто)\"; key = \"lab_w\"; edit_width = 8; }
  }
  ok_cancel;
}")

(defun nvk-nastrojki-dcl-put ( / put f)
  (setq put (strcat (if (getenv "TEMP") (getenv "TEMP") ".") "\\nvk_nastrojki.dcl"))
  (setq f (open put "w"))
  (if f (progn (write-line *nvk-nastrojki-dcl* f) (close f)))
  put)

(defun nvk-joint-idx ( / )
  (cond
    ((equal *nvk-joint* "WELD") 1)
    ((equal *nvk-joint* "COUPLING") 2)
    (t 0)))

(defun nvk-joint-from-idx (i)
  (cond
    ((= i 1) "WELD")
    ((= i 2) "COUPLING")
    (t "SOCKET")))

(defun nvk-slope-unit-idx ( / )
  (cond
    ((equal *nvk-slope-unit* "degrees") 1)
    ((equal *nvk-slope-unit* "ratio") 2)
    ((equal *nvk-slope-unit* "auto") 3)
    (t 0)))

(defun nvk-slope-unit-from-idx (i)
  (cond
    ((= i 1) "degrees")
    ((= i 2) "ratio")
    ((= i 3) "auto")
    (t "permille")))

(defun nvk-nastrojki-accept ( / )
  (setq *nvk-bedding*      (atof (get_tile "bedding"))
        *nvk-thk-zash*     (atof (get_tile "zash"))
        *nvk-thk-asph*     (atof (get_tile "asph"))
        *nvk-thk-gazn*     (atof (get_tile "gazn"))
        *nvk-thk-trot*     (atof (get_tile "trot"))
        *nvk-joint*        (nvk-joint-from-idx (atoi (get_tile "joint")))
        *nvk-cs-slope*     (atof (get_tile "slope"))
        *nvk-cs-ygap*      (atof (get_tile "ygap"))
        *nvk-proj-fact*    (= (get_tile "proj_fact") "1")
        *nvk-single-scatter* (= (get_tile "single_sc") "1")
        *nvk-annotative*   (= (get_tile "annot") "1")
        *nvk-pav-all*      (= (get_tile "pav_all") "1")
        *nvk-pick-zones*   (= (get_tile "pick_z") "1")
        *nvk-zones-str*    (get_tile "zones")
        *nvk-plan-int*     (atof (get_tile "plan_int"))
        *nvk-h-scale*      (atof (get_tile "h_sc"))
        *nvk-l-scale*      (atof (get_tile "l_sc"))
        *nvk-cs-scale*     (atof (get_tile "cs_sc"))
        *nvk-stations-str* (get_tile "stations")
        *nvk-block-cs*     (= (get_tile "block_cs") "1")
        *nvk-block-ep*     (= (get_tile "block_ep") "1")
        *nvk-show-tags*    (= (get_tile "show_tags") "1")
        *nvk-cs-hwidth*    (atof (get_tile "hwidth"))
        *nvk-text-h*       (atof (get_tile "text_h"))
        *nvk-cs-text-h*    (atof (get_tile "cs_th"))
        *nvk-text-off*     (atof (get_tile "text_off"))
        *nvk-round-to*     2
        *nvk-slope-round*  1
        *nvk-slope-unit*   (nvk-slope-unit-from-idx (atoi (get_tile "slope_u")))
        *nvk-show-permille* (= (get_tile "show_perm") "1")
        *nvk-show-len*     T
        *nvk-show-slope*   T
        *nvk-label-col-w*  (atof (get_tile "lab_w")))
  (done_dialog 1))

(defun nvk-dialog-nastrojki ( / put id ok)
  (setq put (nvk-nastrojki-dcl-put)
        ok nil)
  (setq id (load_dialog put))
  (if (not (new_dialog "nvk_nastrojki" id))
    (progn (princ "\n[НВК] диалог настроек не открылся.") nil)
    (progn
      (start_list "joint")
      (add_list "Раструбное (SOCKET)")
      (add_list "Сварное (WELD)")
      (add_list "Муфтовое (COUPLING)")
      (end_list)
      (set_tile "bedding"  (rtos *nvk-bedding* 2 3))
      (set_tile "zash"     (rtos *nvk-thk-zash* 2 3))
      (set_tile "asph"     (rtos *nvk-thk-asph* 2 3))
      (set_tile "gazn"     (rtos (if *nvk-thk-gazn* *nvk-thk-gazn* 0.0) 2 3))
      (set_tile "trot"     (rtos (if *nvk-thk-trot* *nvk-thk-trot* 0.0) 2 3))
      (set_tile "joint"    (itoa (nvk-joint-idx)))
      (set_tile "slope"    (rtos *nvk-cs-slope* 2 3))
      (set_tile "ygap"     (rtos (if *nvk-cs-ygap* *nvk-cs-ygap* 100.0) 2 1))
      (set_tile "proj_fact" (if *nvk-proj-fact* "1" "0"))
      (set_tile "single_sc" (if *nvk-single-scatter* "1" "0"))
      (set_tile "annot"     (if *nvk-annotative* "1" "0"))
      (set_tile "pav_all"   (if *nvk-pav-all* "1" "0"))
      (set_tile "pick_z"    (if *nvk-pick-zones* "1" "0"))
      (set_tile "zones"     (if *nvk-zones-str* *nvk-zones-str* ""))
      (set_tile "plan_int"  (rtos (if *nvk-plan-int* *nvk-plan-int* 50.0) 2 1))
      (set_tile "h_sc"     (rtos *nvk-h-scale* 2 1))
      (set_tile "l_sc"     (rtos *nvk-l-scale* 2 1))
      (set_tile "cs_sc"    (rtos *nvk-cs-scale* 2 1))
      (set_tile "stations" (if *nvk-stations-str* *nvk-stations-str* "AUTO"))
      (set_tile "block_cs" (if *nvk-block-cs* "1" "0"))
      (set_tile "block_ep" (if *nvk-block-ep* "1" "0"))
      (set_tile "show_tags" (if *nvk-show-tags* "1" "0"))
      (set_tile "hwidth"   (rtos *nvk-cs-hwidth* 2 1))
      (start_list "slope_u")
      (add_list "Промилле")
      (add_list "Градусы")
      (add_list "Соотношение сторон")
      (add_list "Авто")
      (end_list)
      (set_tile "text_h"    (rtos *nvk-text-h* 2 1))
      (set_tile "cs_th"     (rtos *nvk-cs-text-h* 2 2))
      (set_tile "text_off"  (rtos *nvk-text-off* 2 0))
      (set_tile "slope_u"   (itoa (nvk-slope-unit-idx)))
      (set_tile "show_perm" (if *nvk-show-permille* "1" "0"))
      (set_tile "lab_w"     (rtos *nvk-label-col-w* 2 1))
      (action_tile "accept" "(nvk-nastrojki-accept)")
      (action_tile "cancel" "(done_dialog 0)")
      (setq ok (start_dialog))
      (unload_dialog id)
      (if (= ok 1)
        (progn
          (if (<= *nvk-bedding* 0.0) (setq *nvk-bedding* 0.10))
          (if (<= *nvk-thk-zash* 0.0) (setq *nvk-thk-zash* 0.30))
          (if (< *nvk-thk-asph* 0.0) (setq *nvk-thk-asph* 0.0))
          (if (< *nvk-thk-gazn* 0.0) (setq *nvk-thk-gazn* 0.0))
          (if (< *nvk-thk-trot* 0.0) (setq *nvk-thk-trot* 0.0))
          (if (< *nvk-cs-ygap* 0.0) (setq *nvk-cs-ygap* 0.0))
          (if (<= *nvk-h-scale* 0.0) (setq *nvk-h-scale* 10.0))
          (if (<= *nvk-l-scale* 0.0) (setq *nvk-l-scale* 1.0))
          (if (<= *nvk-cs-scale* 0.0) (setq *nvk-cs-scale* 1.0))
          (if (<= *nvk-cs-hwidth* 0.05) (setq *nvk-cs-hwidth* 3.0))
          (if (<= *nvk-text-h* 0.0) (setq *nvk-text-h* 2.5))
          (if (<= *nvk-cs-text-h* 0.0) (setq *nvk-cs-text-h* 0.5))
          (if (< *nvk-text-off* 0.0) (setq *nvk-text-off* 0.0))
          (if (> *nvk-text-off* 100.0) (setq *nvk-text-off* 100.0))
          (if (not (member *nvk-round-to* '(2 3))) (setq *nvk-round-to* 2))
          (if (or (< *nvk-slope-round* 0) (> *nvk-slope-round* 2)) (setq *nvk-slope-round* 1))
          (if (< *nvk-label-col-w* 0.0) (setq *nvk-label-col-w* 0.0))
          (nvk-table-apply)
          (if (or (null *nvk-stations-str*) (= (vl-string-trim " " *nvk-stations-str*) ""))
            (setq *nvk-stations-str* "AUTO"))
          (nvk-nod-sohranit)
          (princ "\n[НВК] настройки записаны. НВК-ОБНОВИТЬ — пересчитать.")
          T)
        nil))))

(princ "\n  nvk-nastrojki.lsp")
(princ)
