﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvNetCS
{
    [Serializable]
    public class Trainer
    {
        public Network Net { get; set; }
        public double learning_rate { get; set; }
        public double l1_decay { get; set; }
        public double l2_decay { get; set; }
        public int batch_size { get; set; }
        public TrainingMethod method { get; set; }
        public double momentum { get; set; }
        public double ro { get; set; }// used in adadelta
        public double eps { get; set; }
        public double beta1 { get; set; }
        public double beta2 { get; set; }
        public int k { get; set; }
        public List<double[]> gsum { get; set; }
        public List<double[]> xsum { get; set; }
        public bool Regression { get; set; }
    
        public Trainer()
        {
            learning_rate = 0.01;
            l1_decay = 0.0;
            l2_decay = 0.0;
            batch_size = 51;
            method = TrainingMethod.sgd;
            momentum = 0.9;
            ro = 0.95;
            eps = 1e-8;
            beta1 = 0.9;
            beta2 = 0.999;
            k = 0;
            gsum = new List<double[]>();
            xsum = new List<double[]>();
          
        }
    
        public TrainingResult Train(Vol x,int y)
        {
            if (Net.LossLayer is RegressionLayer)
            {
                this.Regression = true;
            }
            else
            {
                this.Regression = false;
            }
            this.Net.Forward(x, true);

            var cost_loss = this.Net.Backward(y);

            var l2_decay_loss = 0.0;
            var l1_decay_loss = 0.0;

            this.k++;
          
            if(this.k % this.batch_size ==0)
            {
                var pglist = this.Net.GetParamsAndGrads();
                if (this.gsum.Count ==0 && (this.method!=TrainingMethod.sgd || this.momentum>0.0 ) )
                {
                    for (int i = 0; i < pglist.Count; i++)
                    {
                        this.gsum.Add(new double[pglist[i].Params.Length]);
                        if (this.method==TrainingMethod.adam|| this.method == TrainingMethod.adadelta)
                        {
                            this.xsum.Add(new double[pglist[i].Params.Length]);
                        } 
                    }
                }

                for (int i = 0; i < pglist.Count; i++)
                {
                    var pg = pglist[i];
                    var p = pg.Params;
                    var g = pg.Grads;

                    var l2_decay_mul = pg.l2_decay_mul;
                    var l1_decay_mul = pg.l1_decay_mul;
                    var l2_decay = this.l2_decay * l2_decay_mul;
                    var l1_decay = this.l1_decay * l2_decay_mul;

                    var plen = p.Length;
                    for (int j = 0; j < plen; j++)
                    {
                        l2_decay_loss += l2_decay * p[j] * p[j] / 2;
                        l1_decay_loss += l1_decay * Math.Abs(p[j]);
                        var l1grad = l1_decay * (p[j] >0 ? 1 : -1);
                        var l2grad = l2_decay * (p[j]);
                        var gij = (l2grad + l1grad + g[j]) / this.batch_size;

                        var gsumi = this.gsum[i];

                        if(this.method==TrainingMethod.adam)
                        {
                            var xsumi = this.xsum[i];
                            gsumi[j] = gsumi[j] * this.beta1 + (1 - this.beta1) * gij;
                            xsumi[j] = xsumi[j] * this.beta2 + (1 - this.beta2) * gij * gij; // update biased second moment estimate
                            var biasCorr1 = gsumi[j] * (1 - Math.Pow(this.beta1, this.k)); // correct bias first moment estimate
                            var biasCorr2 = xsumi[j] * (1 - Math.Pow(this.beta2, this.k)); // correct bias second moment estimate
                            var dx = -this.learning_rate * biasCorr1 / (Math.Sqrt(biasCorr2) + this.eps);
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.adagrad)
                        {
                            // adagrad update
                            gsumi[j] = gsumi[j] + gij * gij;
                            var dx = -this.learning_rate / Math.Sqrt(gsumi[j] + this.eps) * gij;
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.windowgrad)
                        {
                            // this is adagrad but with a moving window weighted average
                            // so the gradient is not accumulated over the entire history of the run. 
                            // it's also referred to as Idea #1 in Zeiler paper on Adadelta. Seems reasonable to me!
                            gsumi[j] = this.ro * gsumi[j] + (1 - this.ro) * gij * gij;
                            var dx = -this.learning_rate / Math.Sqrt(gsumi[j] + this.eps) * gij; // eps added for better conditioning
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.adadelta)
                        {
                            var xsumi = this.xsum[i];
                            gsumi[j] = this.ro * gsumi[j] + (1 - this.ro) * gij * gij;
                            var dx = -Math.Sqrt((xsumi[j] + this.eps) / (gsumi[j] + this.eps)) * gij;
                            xsumi[j] = this.ro * xsumi[j] + (1 - this.ro) * dx * dx; // yes, xsum lags behind gsum by 1.
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.nesterov)
                        {
                            var dx = gsumi[j];
                            gsumi[j] = gsumi[j] * this.momentum + this.learning_rate * gij;
                            dx = this.momentum * dx - (1.0 + this.momentum) * gsumi[j];
                            p[j] += dx;
                        }
                        else
                        {
                            // assume SGD
                            if (this.momentum > 0.0)
                            {
                                // momentum update
                                var dx = this.momentum * gsumi[j] - this.learning_rate * gij; // step
                                gsumi[j] = dx; // back this up for next iteration of momentum
                                p[j] += dx; // apply corrected gradient
                            }
                            else
                            {
                                // vanilla sgd
                                p[j] += -this.learning_rate * gij;
                            }
                        }
                        g[j] = 0.0; // zero out gradient so that we can begin accumulating anew
         
                    } 
                }
            }
            return new TrainingResult() {l2_decay_loss=l2_decay_loss, l1_decay_loss=l1_decay_loss, 
             cost_loss=cost_loss, softmax_loss = cost_loss, loss=
            cost_loss+l1_decay_loss+l2_decay_loss};
        }
        public TrainingResult Train(Vol x, ClassOutput y)
        {
            if (Net.LossLayer is RegressionLayer)
            {
                this.Regression = true;
            }
            else
            {
                this.Regression = false;
            }
            this.Net.Forward(x, true);

            var cost_loss = this.Net.Backward(y);
            
            var l2_decay_loss = 0.0;
            var l1_decay_loss = 0.0;

            this.k++;
          
            if(this.k % this.batch_size ==0)
            {
                var pglist = this.Net.GetParamsAndGrads();
                this.gsum.Clear();
                for (int i = 0; i < pglist.Count; i++)
                {
                    this.gsum.Add(new double[pglist[i].Params.Length]);
                    
                }
                if (this.gsum.Count ==0 && (this.method!=TrainingMethod.sgd || this.momentum>0.0 ) )
                {
                    for (int i = 0; i < pglist.Count; i++)
                    { 
                        if (this.method==TrainingMethod.adam|| this.method == TrainingMethod.adadelta)
                        {
                            this.xsum.Add(new double[pglist[i].Params.Length]);
                        } 
                    }
                }

                for (int i = 0; i < pglist.Count; i++)
                {
                    var pg = pglist[i];
                    var p = pg.Params;
                    var g = pg.Grads;

                    var l2_decay_mul = pg.l2_decay_mul;
                    var l1_decay_mul = pg.l1_decay_mul;
                    var l2_decay = this.l2_decay * l2_decay_mul;
                    var l1_decay = this.l1_decay * l2_decay_mul;

                    var plen = p.Length;
                    for (int j = 0; j < plen; j++)
                    {
                        l2_decay_loss += l2_decay * p[j] * p[j] / 2;
                        l1_decay_loss += l1_decay * Math.Abs(p[j]);
                        var l1grad = l1_decay * (p[j] >0 ? 1 : -1);
                        var l2grad = l2_decay * (p[j]);
                        var gij = (l2grad + l1grad + g[j]) / this.batch_size;

                        var gsumi = this.gsum[i];

                        if(this.method==TrainingMethod.adam)
                        {
                            var xsumi = this.xsum[i];
                            gsumi[j] = gsumi[j] * this.beta1 + (1 - this.beta1) * gij;
                            xsumi[j] = xsumi[j] * this.beta2 + (1 - this.beta2) * gij * gij; // update biased second moment estimate
                            var biasCorr1 = gsumi[j] * (1 - Math.Pow(this.beta1, this.k)); // correct bias first moment estimate
                            var biasCorr2 = xsumi[j] * (1 - Math.Pow(this.beta2, this.k)); // correct bias second moment estimate
                            var dx = -this.learning_rate * biasCorr1 / (Math.Sqrt(biasCorr2) + this.eps);
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.adagrad)
                        {
                            // adagrad update
                            gsumi[j] = gsumi[j] + gij * gij;
                            var dx = -this.learning_rate / Math.Sqrt(gsumi[j] + this.eps) * gij;
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.windowgrad)
                        {
                            // this is adagrad but with a moving window weighted average
                            // so the gradient is not accumulated over the entire history of the run. 
                            // it's also referred to as Idea #1 in Zeiler paper on Adadelta. Seems reasonable to me!
                            gsumi[j] = this.ro * gsumi[j] + (1 - this.ro) * gij * gij;
                            var dx = -this.learning_rate / Math.Sqrt(gsumi[j] + this.eps) * gij; // eps added for better conditioning
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.adadelta)
                        {
                            var xsumi = this.xsum[i];
                            gsumi[j] = this.ro * gsumi[j] + (1 - this.ro) * gij * gij;
                            var dx = -Math.Sqrt((xsumi[j] + this.eps) / (gsumi[j] + this.eps)) * gij;
                            xsumi[j] = this.ro * xsumi[j] + (1 - this.ro) * dx * dx; // yes, xsum lags behind gsum by 1.
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.nesterov)
                        {
                            var dx = gsumi[j];
                            gsumi[j] = gsumi[j] * this.momentum + this.learning_rate * gij;
                            dx = this.momentum * dx - (1.0 + this.momentum) * gsumi[j];
                            p[j] += dx;
                        }
                        else
                        {
                            // assume SGD
                            if (this.momentum > 0.0)
                            {
                                // momentum update
                                var dx = this.momentum * gsumi[j] - this.learning_rate * gij; // step
                                gsumi[j] = dx; // back this up for next iteration of momentum
                                p[j] += dx; // apply corrected gradient
                            }
                            else
                            {
                                // vanilla sgd
                                p[j] += -this.learning_rate * gij;
                            }
                        }
                        g[j] = 0.0; // zero out gradient so that we can begin accumulating anew
         
                    } 
                }
            }
            return new TrainingResult() {l2_decay_loss=l2_decay_loss, l1_decay_loss=l1_decay_loss, 
             cost_loss=cost_loss, softmax_loss = cost_loss, loss=
            cost_loss+l1_decay_loss+l2_decay_loss};
        }



        public TrainingResult Train(Vol x, double[] y)
        {
            if (Net.LossLayer is RegressionLayer)
            {
                this.Regression = true;
            }
            else
            {
                this.Regression = false;
            }
            this.Net.Forward(x, true);

            var cost_loss = this.Net.Backward(y);

            var l2_decay_loss = 0.0;
            var l1_decay_loss = 0.0;

            this.k++;

            // initialize lists for accumulators. Will only be done once on first iteration
            if (this.k % this.batch_size == 0)
            {
                // only vanilla sgd doesnt need either lists
                // momentum needs gsum
                // adagrad needs gsum
                // adadelta needs gsum and xsu
                var pglist = this.Net.GetParamsAndGrads();
                if (this.gsum.Count == 0 && (this.method != TrainingMethod.sgd || this.momentum > 0.0))
                {
                    for (int i = 0; i < pglist.Count; i++)
                    {
                        this.gsum.Add(new double[pglist[i].Params.Length]);
                        if (this.method == TrainingMethod.adam || this.method == TrainingMethod.adadelta)
                        {
                            this.xsum.Add(new double[pglist[i].Params.Length]);// conserve memory
                        }
                      
                    }
                }

                for (int i = 0; i < pglist.Count; i++)
                {
                    var pg = pglist[i];
                    var p = pg.Params;
                    var g = pg.Grads;

                    var l2_decay_mul = pg.l2_decay_mul;
                    var l1_decay_mul = pg.l1_decay_mul;
                    var l2_decay = this.l2_decay * l2_decay_mul;
                    var l1_decay = this.l1_decay * l2_decay_mul;

                    var plen = p.Length;
                    for (int j = 0; j < plen; j++)
                    {
                        l2_decay_loss += l2_decay * p[j] * p[j] / 2;
                        l1_decay_loss += l1_decay * Math.Abs(p[j]);
                        var l1grad = l1_decay * (p[j] > 0 ? 1 : -1);
                        var l2grad = l2_decay * (p[j]);
                        var gij = (l2grad + l1grad + g[j]) / this.batch_size;

                        var gsumi = this.gsum[i];

                        if (this.method == TrainingMethod.adam)
                        {
                            var xsumi = this.xsum[i];
                            gsumi[j] = gsumi[j] * this.beta1 + (1 - this.beta1) * gij;
                            xsumi[j] = xsumi[j] * this.beta2 + (1 - this.beta2) * gij * gij; // update biased second moment estimate
                            var biasCorr1 = gsumi[j] * (1 - Math.Pow(this.beta1, this.k)); // correct bias first moment estimate
                            var biasCorr2 = xsumi[j] * (1 - Math.Pow(this.beta2, this.k)); // correct bias second moment estimate
                            var dx = -this.learning_rate * biasCorr1 / (Math.Sqrt(biasCorr2) + this.eps);
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.adagrad)
                        {
                            // adagrad update
                            gsumi[j] = gsumi[j] + gij * gij;
                            var dx = -this.learning_rate / Math.Sqrt(gsumi[j] + this.eps) * gij;
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.windowgrad)
                        {
                            // this is adagrad but with a moving window weighted average
                            // so the gradient is not accumulated over the entire history of the run. 
                            // it's also referred to as Idea #1 in Zeiler paper on Adadelta. Seems reasonable to me!
                            gsumi[j] = this.ro * gsumi[j] + (1 - this.ro) * gij * gij;
                            var dx = -this.learning_rate / Math.Sqrt(gsumi[j] + this.eps) * gij; // eps added for better conditioning
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.adadelta)
                        {
                            var xsumi = this.xsum[i];
                            gsumi[j] = this.ro * gsumi[j] + (1 - this.ro) * gij * gij;
                            var dx = -Math.Sqrt((xsumi[j] + this.eps) / (gsumi[j] + this.eps)) * gij;
                            xsumi[j] = this.ro * xsumi[j] + (1 - this.ro) * dx * dx; // yes, xsum lags behind gsum by 1.
                            p[j] += dx;
                        }
                        else if (this.method == TrainingMethod.nesterov)
                        {
                            var dx = gsumi[j];
                            gsumi[j] = gsumi[j] * this.momentum + this.learning_rate * gij;
                            dx = this.momentum * dx - (1.0 + this.momentum) * gsumi[j];
                            p[j] += dx;
                        }
                        else
                        {
                            // assume SGD
                            if (this.momentum > 0.0)
                            {
                                // momentum update
                                var dx = this.momentum * gsumi[j] - this.learning_rate * gij; // step
                                gsumi[j] = dx; // back this up for next iteration of momentum
                                p[j] += dx; // apply corrected gradient
                            }
                            else
                            {
                                // vanilla sgd
                                p[j] += -this.learning_rate * gij;
                            }
                        }
                        g[j] = 0.0; // zero out gradient so that we can begin accumulating anew

                    }
                }
            }
            return new TrainingResult()
            {
                l2_decay_loss = l2_decay_loss,
                l1_decay_loss = l1_decay_loss,
                cost_loss = cost_loss,
                softmax_loss = cost_loss,
                loss =
                    cost_loss + l1_decay_loss + l2_decay_loss
            };
        }
    }
    public  class TrainingResult
    {
        public double l2_decay_loss { get; set; }
        public double l1_decay_loss { get; set; }
        public double cost_loss { get; set; }
        public double softmax_loss { get; set; }
        public double loss { get; set; }

    }
    public enum TrainingMethod
    {
        sgd,adam,adagrad,adadelta,windowgrad,nesterov
    }
}
