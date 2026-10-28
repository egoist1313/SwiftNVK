;;; nvk-config.lsp — константы Поперечника НВК
;;; Зависит от: ничего.

(vl-load-com)

(setq *nvk-versija*        "0.250")

(defun nvk-def (sym val)
  (if (not (boundp sym)) (set sym val)))
(setq *nvk-seg-id*         "")
(setq *nvk-appid*          "NVK")
(setq *nvk-dll-name*       "Nvk.Cad.dll")

;; Этапы конвейера. Порядок = порядок ключей в *nvk-stage-order*.
(setq *nvk-stage-order*
  '("CONFIG" "INPUT" "CHAIN" "STATIONS" "SURF" "GEOM-CS"
    "CS-DRAW" "EP" "POPER" "VOL" "PLAN" "SHEETS"))

;; Политика блоков по умолчанию (T = блок, nil = loose).
(setq *nvk-block-cs*   T)
(setq *nvk-block-ep*   T)
(nvk-def '*nvk-show-tags* T)
(setq *nvk-block-poper* nil)
(if (not (boundp '*nvk-ep-lpts*)) (setq *nvk-ep-lpts* (list nil nil nil nil nil)))
(if (not (boundp '*nvk-cs-lpts*)) (setq *nvk-cs-lpts* (list nil nil nil nil nil)))

;; Толщины и траншея — дефолты как в PipeBuild.
(setq *nvk-bedding*    0.10)
(setq *nvk-thk-zash*   0.30)
(nvk-def '*nvk-thk-asph*   1.02)   ; каталог ASPH; на трассу — через зоны или PAV_ALL
(nvk-def '*nvk-thk-gazn*   0.15)
(nvk-def '*nvk-thk-trot*   0.31)
(nvk-def '*nvk-thk-other*  0.35)
(nvk-def '*nvk-fill-asph*  "SAND")
(nvk-def '*nvk-fill-trot*  "SAND")
(nvk-def '*nvk-fill-gazn*  "SOIL")
(nvk-def '*nvk-fill-other* "SOIL")
(nvk-def '*nvk-use-black-earth* nil)
(nvk-def '*nvk-black-earth-h* "")
(nvk-def '*nvk-black-earth-n* "")
(setq *nvk-l1-mh* nil)
(setq *nvk-table-off-m* 1.0)
(setq *nvk-l1-preset* "default")
(nvk-def '*nvk-pav-all*    nil)
(nvk-def '*nvk-pick-zones* nil)
(nvk-def '*nvk-zones-str*  "")
(nvk-def '*nvk-use-wall*   T)
(nvk-def '*nvk-wall-str*   "")
(setq *nvk-joint*      "SOCKET")
(setq *nvk-cs-hwidth*  3.0)
(setq *nvk-cs-slope*   0.5)    ; как DCL PipeBuild
(setq *nvk-proj-fact*       nil)
(setq *nvk-single-scatter*  T)
(setq *nvk-use-sc-r*        T)
(setq *nvk-use-sc-t*        nil)
(setq *nvk-use-sc-z*        T)
(setq *nvk-use-sc-s*        T)
(setq *nvk-sc-r*            0.05)
(setq *nvk-sc-t*            0.005)
(setq *nvk-sc-z*            0.05)
(setq *nvk-sc-s*            0.05)
(setq *nvk-annotative*      nil)
(setq *nvk-ann-gap*         0.0)    ; метры ниже условного горизонта
(setq *nvk-plan-en*         T)
(setq *nvk-plan-int*        50.0)
(setq *nvk-plan-min*        15.0)
(setq *nvk-draw-proj*       T)
(setq *nvk-use-th*          T)
(setq *nvk-delta-h*         T)
(setq *nvk-lab-h*           "")
(setq *nvk-lab-l*           "")
(setq *nvk-lab-s*           "")
(setq *nvk-scatter-vals*    "")
(nvk-def '*nvk-scatter-l2*  "")
(nvk-def '*nvk-scatter-l3*  "")
(nvk-def '*nvk-scatter-l4*  "")
(nvk-def '*nvk-scatter-l5*  "")
(setq *nvk-latoff-vals*     "")
(setq *nvk-plan-tgt*        "")
(setq *nvk-plan-lat*        "")
(setq *nvk-cs-scale*   1.0)    ; как l_scale в PipeBuild: 1 м = 1 ед. (изотропно)
(setq *nvk-cs-text-h*  0.5)    ; текст таблиц сечения, отдельно от профиля
(setq *nvk-cs-ygap*    100.0)  ; шаг между уровнями L1…L5 / отступ как CS_YGAP
(setq *nvk-cs-cols*    2)      ; сечений в ряду (как 43-44-46)

(setq *nvk-osh-override* nil)    ; nil = авто, число = ручной условный горизонт
(setq *nvk-h-scale*    10.0)
(setq *nvk-l-scale*    1.0)
;; Таблица — те же ключи, что PoperSwift.
(setq *nvk-text-h*        2.5)
(setq *nvk-text-off*      10.0)   ; % от высоты текста
(setq *nvk-round-to*      2)
(setq *nvk-slope-round*   1)
(setq *nvk-slope-unit*    "permille")
(setq *nvk-show-permille* T)
(setq *nvk-show-len*      T)
(setq *nvk-show-slope*    T)
(setq *nvk-label-col-w*   0.0)    ; 0 = авто

;; Оркестратор
(defun nvk-table-apply ( / r)
  (setq r
    (vl-catch-all-apply 'nvk-table-cfg
      (list *nvk-text-h*
            *nvk-text-off*
            *nvk-round-to*
            *nvk-slope-round*
            *nvk-slope-unit*
            (if *nvk-show-permille* 1 0)
            (if *nvk-show-len* 1 0)
            (if *nvk-show-slope* 1 0)
            *nvk-label-col-w*
            *nvk-cs-text-h*)))
  (vl-catch-all-apply 'nvk-ui-otdat nil)
  (if (and *nvk-osh-override* (numberp *nvk-osh-override*))
    (vl-catch-all-apply 'nvk-osh-override (list *nvk-osh-override*))
    (vl-catch-all-apply 'nvk-osh-override nil))
  (vl-catch-all-apply 'nvk-cache-put
    (list "SCATTER"  (if *nvk-scatter-vals* *nvk-scatter-vals* "")
          "SCATTER2" (if *nvk-scatter-l2* *nvk-scatter-l2* "")
          "SCATTER3" (if *nvk-scatter-l3* *nvk-scatter-l3* "")
          "SCATTER4" (if *nvk-scatter-l4* *nvk-scatter-l4* "")
          "SCATTER5" (if *nvk-scatter-l5* *nvk-scatter-l5* "")
          "LATOFF"   (if *nvk-latoff-vals* *nvk-latoff-vals* "")
          "PLAN_TGT" (if *nvk-plan-tgt* *nvk-plan-tgt* "")
          "PLAN_LAT" (if *nvk-plan-lat* *nvk-plan-lat* "")
          "ZONES"    (if *nvk-zones-str* *nvk-zones-str* "")))
  (not (vl-catch-all-error-p r)))

(setq *nvk-do-cs*      T)
(setq *nvk-do-ep*      T)
(setq *nvk-do-poper*   T)
(setq *nvk-do-vol*     T)
(setq *nvk-do-plan*    T)

(setq *nvk-run-id*     0)
(setq *nvk-dll-ok*     nil)

;; Конфигурации строк таблицы по уровням (сериализованные, как TBL_ROWS_L*)
(nvk-def '*nvk-rows-l1* "")
(nvk-def '*nvk-rows-l2* "")
(nvk-def '*nvk-rows-l3* "")
(nvk-def '*nvk-rows-l4* "")
(nvk-def '*nvk-rows-l5* "")

;; Handles Civil 3D Profile-объектов для строк «факт» каждого уровня
(nvk-def '*nvk-fact-prof-l1* "")
(nvk-def '*nvk-fact-prof-l2* "")
(nvk-def '*nvk-fact-prof-l3* "")
(nvk-def '*nvk-fact-prof-l4* "")
(nvk-def '*nvk-fact-prof-l5* "")

(princ "\n  nvk-config.lsp")
(princ)
