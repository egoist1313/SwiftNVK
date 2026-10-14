;;; nvk-orchestrator.lsp — конвейер этапов с логом OK/SKIP/FAIL
;;; Зависит от: nvk-config.

(setq *nvk-stages*
  (list
    (cons "CONFIG"   'nvk-stage-config)
    (cons "INPUT"    'nvk-stage-input)
    (cons "CHAIN"    'nvk-stage-chain)
    (cons "STATIONS" 'nvk-stage-stations)
    (cons "SURF"     'nvk-stage-surf)
    (cons "GEOM-CS"  'nvk-stage-geom-cs)
    (cons "CS-DRAW"  'nvk-stage-cs-draw)
    (cons "EP"       'nvk-stage-ep)
    (cons "POPER"    'nvk-stage-poper)
    (cons "VOL"      'nvk-stage-vol)
    (cons "PLAN"     'nvk-stage-plan)))

(defun nvk-stage-config ( / )
  (nvk-registr-appid)
  (nvk-sozdat-vse-sloi)
  (nvk-table-apply)
  "AppID NVK, слои NVK-*, таблица PoperSwift")



(defun nvk-log-path ( )
  (strcat *nvk-dir* "\\nvk-log.txt"))

;; Только AutoLISP open/write-line. .NET LispFunction нельзя звать через
;; vl-catch-all-apply — Civil орёт «неверная функция: NVK-LOG-BEGIN» на unlock.
(defun nvk-log-write (s / f p)
  (setq p (nvk-log-path)
        f (open p "a"))
  (if f
    (progn
      (write-line (if s s "") f)
      (close f)))
  (princ))

(defun nvk-log-reset (n / f p)
  (setq p (nvk-log-path)
        f (open p "w"))
  (if f
    (progn
      (write-line (strcat "========== НВК прогон #" (itoa n) " ==========") f)
      (write-line (strcat "версия " *nvk-versija*) f)
      (write-line (strcat "файл " p) f)
      (close f))
    (princ (strcat "\n[НВК] не открыть лог: " p)))
  (princ))

(defun nvk-log-line (imja status text)
  (setq text (if text text ""))
  (nvk-log-write
    (strcat (cond
              ((= status "OK")   "  OK   ")
              ((= status "SKIP") " SKIP  ")
              ((= status "FAIL") " FAIL  ")
              (t                 "  ..   "))
            imja
            (if (> (strlen text) 0) (strcat "  |  " text) "")))
  (if (or (= status "FAIL") (= status "SKIP"))
    (princ (strcat "\n[НВК] " status "  " imja
                   (if (> (strlen text) 0) (strcat "  |  " text) ""))))
  (princ))

(defun nvk-rez-status (rez / )
  (cond
    ((null rez) "FAIL")
    ((and (listp rez) (eq (car rez) 'SKIP)) "SKIP")
    ((vl-catch-all-error-p rez) "FAIL")
    (t "OK")))

(defun nvk-rez-text (rez / )
  (cond
    ((vl-catch-all-error-p rez) (vl-catch-all-error-message rez))
    ((and (listp rez) (eq (car rez) 'SKIP))
     (if (eq (type (cdr rez)) 'STR) (cdr rez) "ещё не собран"))
    ((eq rez T) "")
    ((eq rez nil) "ошибка")
    ((listp rez) (vl-princ-to-string rez))
    ((eq (type rez) 'STR) rez)
    (t (vl-princ-to-string rez))))

(defun nvk-etap-vkl-p (imja)
  (cond
    ((member imja '("CONFIG" "INPUT" "CHAIN" "STATIONS" "SURF" "GEOM-CS")) T)
    ((= imja "CS-DRAW") *nvk-do-cs*)
    ((= imja "EP")      *nvk-do-ep*)
    ((= imja "POPER")   *nvk-do-poper*)
    ((= imja "VOL")     *nvk-do-vol*)
    ((= imja "PLAN")    *nvk-do-plan*)
    (t T)))

(defun nvk-zapustit-etap (imja / par fn rez)
  (setq par (assoc imja *nvk-stages*))
  (cond
    ((null par)
     (nvk-log-line imja "FAIL" "нет в реестре")
     "FAIL")
    ((null (nvk-etap-vkl-p imja))
     (nvk-log-line imja "SKIP" "выкл. на панели")
     "SKIP")
    ((null (cdr par))
     (nvk-log-line imja "SKIP" "ещё не собран")
     "SKIP")
    (t
     (setq fn (cdr par))
     (nvk-log-write (strcat "---- " imja " ----"))
     (setq rez (vl-catch-all-apply fn nil))
     (nvk-log-line imja (nvk-rez-status rez) (nvk-rez-text rez))
     (nvk-rez-status rez))))

(defun nvk-progon ( / imja st n-ok n-skip n-fail)
  (setq *nvk-run-id* (1+ *nvk-run-id*)
        n-ok 0 n-skip 0 n-fail 0)
  (setq *nvk-log-file* (nvk-log-path))
  (nvk-log-reset *nvk-run-id*)
  (princ (strcat "\n[НВК] ПОСТРОИТЬ #" (itoa *nvk-run-id*)
                 "  лог: " *nvk-log-file*))
  (foreach imja *nvk-stage-order*
    (setq st (nvk-zapustit-etap imja))
    (cond
      ((= st "OK")   (setq n-ok   (1+ n-ok)))
      ((= st "SKIP") (setq n-skip (1+ n-skip)))
      (t             (setq n-fail (1+ n-fail)))))
  (princ (strcat "\n[НВК] ========== готово   OK " (itoa n-ok)
                 "  SKIP " (itoa n-skip)
                 "  FAIL " (itoa n-fail)
                 " =========="))
  (vl-catch-all-apply 'nvk-layout-pull nil)
  (vl-catch-all-apply 'nvk-nod-sohranit nil)
  (princ)
  T)

(princ "\n  nvk-orchestrator.lsp")
(princ)
