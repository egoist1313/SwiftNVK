;;; nvk-cs.lsp — этап CS-DRAW: поперечники (блок или россыпь)
;;; Зависит от: nvk-config, nvk-dll, nvk-input.

(if (not (boundp '*nvk-cs-ins*)) (setq *nvk-cs-ins* nil))

(defun nvk-stage-cs-draw ( / p r)
  (cond
    ((not (nvk-dll-est-p))
     nil)
    (t
     (if (null *nvk-cs-ins*)
       (progn
         (princ "\n[НВК] укажи точку вставки поперечников (пустое место на чертеже)")
         (setq p (getpoint "\n[НВК] CS-DRAW точка: "))
         (if p (setq *nvk-cs-ins* p))))
     (if (null *nvk-cs-ins*)
       nil
       (progn
         (nvk-table-apply)
         (nvk-layout-send)
         (setq r
           (vl-catch-all-apply 'nvk-cs-draw
             (list (car *nvk-cs-ins*)
                   (cadr *nvk-cs-ins*)
                   *nvk-cs-scale*
                   *nvk-cs-slope*
                   (if *nvk-block-cs* 1 0)
                   (if *nvk-cs-ygap* *nvk-cs-ygap* 100.0))))
         (cond
           ((vl-catch-all-error-p r) nil)
           ((null r) nil)
           (t r)))))))

(princ "\n  nvk-cs.lsp")
(princ)
