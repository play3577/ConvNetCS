﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvNetCS
{
    [Serializable]
    public class Network
    {
        public List<ILayer> Layers { get; set; }
        public ILossLayer LossLayer { get; set; }
        public List<Vol> others = new List<Vol>();
        public Network()
        {
            Layers = new List<ILayer>();
        }
        public Vol Forward (Vol V, bool is_training)
        {

            var act = this.Layers[0].Forward(V, is_training);
            for (int i = 1; i < this.Layers.Count; i++)
            {
                act = this.Layers[i].Forward(act, is_training);

            }
            act = this.LossLayer.Forward(act, is_training);
            return act;
        }


        public double Backward(int y)
        {
            var loss = this.LossLayer.Backward(y);

            for (int i = Layers.Count-1; i >= 0; i--)
            {
                Layers[i].Backward();
            }

            return loss;
        }

        public double Backward(double[] y)
        {
            var loss = (this.LossLayer as RegressionLayer).Backward(y);

            for (int i = Layers.Count - 1; i >= 0; i--)
            {
                Layers[i].Backward();
            }

            return loss;
        }
        
        public double Backward(ClassOutput y)
        {
            var loss = (this.LossLayer as RegressionLayer).Backward(y);

            for (int i = Layers.Count-1; i >= 0; i--)
            {
                Layers[i].Backward();
            }

            return loss;
        }
        
        public List<ParamsAndGrads> GetParamsAndGrads()
        {
            List<ParamsAndGrads> response = new List<ParamsAndGrads>();
            for (int i = 0; i < Layers.Count; i++)
            {
                response.AddRange(Layers[i].getParamsAndGrads());

            }
            response.AddRange(LossLayer.getParamsAndGrads());
            foreach (var item in others)
            {
                response.Add(new ParamsAndGrads() {  Grads=item.DW, Params=item.W, l1_decay_mul=0, l2_decay_mul=0});
            }
            return response;
        }

        public int GetPrediction()
        {
            var s = this.LossLayer;
            var p = s.Output.W;
            var maxv = p[0];
            var maxi = 0;
            for (int i = 1; i < p.Length; i++)
            {
                if (p[i]>maxv)
                {
                    maxv = p[i];
                    maxi = i;
                }
            }
            return maxi;
        }

        public int GetPrediction( double temp)
        {
            var s = this.LossLayer;
            var p = s.Output.W;
            var maxv = p[0];
            var maxi = 0;
            for (int i = 1; i < p.Length; i++)
            {
                var px = p[i] / temp;
                if (p[i] > maxv)
                {
                    maxv = px;
                    maxi = i;
                }
            }
            return maxi;
        }


      
    }
}
