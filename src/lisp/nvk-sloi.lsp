;;; nvk-sloi.lsp — слои NVK-*
;;; Зависит от: nvk-config.

;; (имя цвет noplot off)
(setq *nvk-sloi*
  '(("NVK-INS"      7 nil nil)
    ("NVK-EP-RAZR"  4 nil nil)
    ("NVK-EP-PESOK" 2 nil nil)
    ("NVK-EP-TRUBA" 5 nil nil)
    ("NVK-EP-ZASH"  6 nil nil)
    ("NVK-EP-ZAS"   3 nil nil)
    ("NVK-CS"       1 nil nil)
    ("NVK-CS-TBL"   7 nil nil)
    ("NVK-POPER"    7 nil nil)
    ("NVK-PLAN"     1 nil nil)
    ("NVK-VOL"      7 nil nil)
    ("NVK-MARK"     1 nil nil)
    ("NVK-HATCH"    8 nil nil)
    ("NVK-HATCHBND" 8 T   T)
    ("NVK-DATA"     8 T   T)))

(defun nvk-sozdat-sloj (naim tsvet noplot off / td)
  (setq td (tblsearch "LAYER" naim))
  (if (not td)
    (entmake
      (list
        '(0 . "LAYER")
        '(100 . "AcDbSymbolTableRecord")
        '(100 . "AcDbLayerTableRecord")
        (cons 2 naim)
        (cons 70 (if off 1 0))
        (cons 62 tsvet)
        '(6 . "Continuous")
        (cons 290 (if noplot 0 1))
        '(370 . -3))))
  t)

(defun nvk-sozdat-vse-sloi ( / rec)
  (foreach rec *nvk-sloi*
    (nvk-sozdat-sloj (nth 0 rec) (nth 1 rec) (nth 2 rec) (nth 3 rec)))
  t)

(defun nvk-registr-appid ( / )
  (if (not (tblsearch "APPID" *nvk-appid*))
    (entmake
      (list
        '(0 . "APPID")
        '(100 . "AcDbSymbolTableRecord")
        '(100 . "AcDbRegAppTableRecord")
        (cons 2 *nvk-appid*)
        '(70 . 0))))
  t)

(princ "\n  nvk-sloi.lsp")
(princ)
